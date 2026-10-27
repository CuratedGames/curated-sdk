//! Walk a content directory, build a zip artifact, and collect a file manifest.

use sha2::{Digest, Sha256};
use std::fs::{self, File};
use std::io::{self, Read};
use std::path::{Path, PathBuf};
use walkdir::WalkDir;
use zip::write::SimpleFileOptions;
use zip::CompressionMethod;
use zip::ZipWriter;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ManifestEntry {
    pub path: String,
    pub sha256: String,
    pub size_bytes: u64,
}

#[derive(Debug, Clone)]
pub struct PackedArtifact {
    pub zip_path: PathBuf,
    pub sha256: String,
    pub size_bytes: u64,
    pub files: Vec<ManifestEntry>,
}

#[derive(Debug)]
pub enum PackError {
    Io(io::Error),
    Message(String),
}

impl std::fmt::Display for PackError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            PackError::Io(e) => write!(f, "{e}"),
            PackError::Message(m) => write!(f, "{m}"),
        }
    }
}

impl std::error::Error for PackError {}

impl From<io::Error> for PackError {
    fn from(value: io::Error) -> Self {
        PackError::Io(value)
    }
}

impl From<zip::result::ZipError> for PackError {
    fn from(value: zip::result::ZipError) -> Self {
        PackError::Message(value.to_string())
    }
}

/// Forward-slash relative path from `root` to `file` (depot-style).
pub fn relative_posix_path(root: &Path, file: &Path) -> Result<String, PackError> {
    let rel = file.strip_prefix(root).map_err(|_| {
        PackError::Message(format!(
            "file {} is outside content root {}",
            file.display(),
            root.display()
        ))
    })?;
    let s = rel
        .components()
        .map(|c| c.as_os_str().to_string_lossy())
        .collect::<Vec<_>>()
        .join("/");
    if s.is_empty() {
        return Err(PackError::Message("empty relative path".into()));
    }
    Ok(s)
}

pub fn file_sha256_hex(path: &Path) -> Result<String, PackError> {
    let mut file = File::open(path)?;
    let mut hasher = Sha256::new();
    let mut buf = [0u8; 64 * 1024];
    loop {
        let n = file.read(&mut buf)?;
        if n == 0 {
            break;
        }
        hasher.update(&buf[..n]);
    }
    Ok(hex::encode(hasher.finalize()))
}

pub fn walk_manifest_files(content_root: &Path) -> Result<Vec<ManifestEntry>, PackError> {
    if !content_root.is_dir() {
        return Err(PackError::Message(format!(
            "content root is not a directory: {}",
            content_root.display()
        )));
    }
    let mut files = Vec::new();
    for entry in WalkDir::new(content_root)
        .follow_links(false)
        .into_iter()
        .filter_map(|e| e.ok())
    {
        if !entry.file_type().is_file() {
            continue;
        }
        let path = entry.path();
        let rel = relative_posix_path(content_root, path)?;
        let meta = fs::metadata(path)?;
        files.push(ManifestEntry {
            path: rel,
            sha256: file_sha256_hex(path)?,
            size_bytes: meta.len(),
        });
    }
    files.sort_by(|a, b| a.path.cmp(&b.path));
    Ok(files)
}

/// Pack all files under `content_root` into `zip_path` (created/overwritten).
pub fn pack_content_directory(
    content_root: &Path,
    zip_path: &Path,
) -> Result<PackedArtifact, PackError> {
    let files = walk_manifest_files(content_root)?;
    if files.is_empty() {
        return Err(PackError::Message(
            "content directory has no files to upload".into(),
        ));
    }
    if let Some(parent) = zip_path.parent() {
        fs::create_dir_all(parent)?;
    }
    let zip_file = File::create(zip_path)?;
    let mut zip = ZipWriter::new(zip_file);
    let opts = SimpleFileOptions::default().compression_method(CompressionMethod::Deflated);
    for entry in &files {
        let abs = content_root.join(entry.path.replace('/', std::path::MAIN_SEPARATOR_STR));
        zip.start_file(&entry.path, opts)?;
        let mut f = File::open(&abs)?;
        io::copy(&mut f, &mut zip)?;
    }
    zip.finish()?;
    let sha256 = file_sha256_hex(zip_path)?;
    let size_bytes = fs::metadata(zip_path)?.len();
    Ok(PackedArtifact {
        zip_path: zip_path.to_path_buf(),
        sha256,
        size_bytes,
        files,
    })
}

/// Stream file bytes in fixed-size chunks (last chunk may be shorter).
/// Prefer [`read_file_chunk_at`] for parallel uploads of large depots.
#[allow(dead_code)]
pub fn read_file_chunks(path: &Path, chunk_size: usize) -> Result<Vec<Vec<u8>>, PackError> {
    if chunk_size == 0 {
        return Err(PackError::Message("chunk size must be > 0".into()));
    }
    let mut file = File::open(path)?;
    let mut chunks = Vec::new();
    loop {
        let mut buf = vec![0u8; chunk_size];
        let mut filled = 0usize;
        while filled < chunk_size {
            let n = file.read(&mut buf[filled..])?;
            if n == 0 {
                break;
            }
            filled += n;
        }
        if filled == 0 {
            break;
        }
        buf.truncate(filled);
        chunks.push(buf);
    }
    Ok(chunks)
}

/// Read one chunk at `offset` (up to `max_len` bytes). Used for parallel PUTs
/// without loading the whole depot into memory.
pub fn read_file_chunk_at(
    path: &Path,
    offset: u64,
    max_len: usize,
) -> Result<Vec<u8>, PackError> {
    use std::io::{Read, Seek, SeekFrom};
    if max_len == 0 {
        return Ok(Vec::new());
    }
    let mut file = File::open(path)?;
    file.seek(SeekFrom::Start(offset))?;
    let mut buf = vec![0u8; max_len];
    let mut filled = 0usize;
    while filled < max_len {
        let n = file.read(&mut buf[filled..])?;
        if n == 0 {
            break;
        }
        filled += n;
    }
    buf.truncate(filled);
    Ok(buf)
}

/// Expected byte length of chunk `index` for a payload of `total_bytes`.
pub fn chunk_len_at(total_bytes: u64, chunk_size: u64, index: u32, chunk_count: u32) -> u64 {
    if total_bytes == 0 || chunk_count == 0 {
        return 0;
    }
    if index + 1 == chunk_count {
        let rem = total_bytes % chunk_size;
        if rem == 0 {
            chunk_size
        } else {
            rem
        }
    } else {
        chunk_size
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use tempfile::tempdir;

    #[test]
    fn relative_path_uses_forward_slashes() {
        let dir = tempdir().unwrap();
        let nested = dir.path().join("bin");
        fs::create_dir_all(&nested).unwrap();
        let file = nested.join("game.exe");
        File::create(&file).unwrap();
        let rel = relative_posix_path(dir.path(), &file).unwrap();
        assert_eq!(rel, "bin/game.exe");
    }

    #[test]
    fn pack_zip_and_manifest() {
        let dir = tempdir().unwrap();
        let content = dir.path().join("content");
        fs::create_dir_all(content.join("bin")).unwrap();
        let mut f = File::create(content.join("bin").join("game.exe")).unwrap();
        f.write_all(b"hello-curated").unwrap();
        let out = dir.path().join("out").join("depot.zip");
        let packed = pack_content_directory(&content, &out).unwrap();
        assert!(out.exists());
        assert_eq!(packed.files.len(), 1);
        assert_eq!(packed.files[0].path, "bin/game.exe");
        assert_eq!(packed.size_bytes, fs::metadata(&out).unwrap().len());
        assert_eq!(packed.sha256.len(), 64);

        let chunks = read_file_chunks(&out, 4).unwrap();
        assert!(chunks.len() >= 2);
        let mut rebuilt = Vec::new();
        for c in chunks {
            rebuilt.extend_from_slice(&c);
        }
        assert_eq!(rebuilt, fs::read(&out).unwrap());

        let total = packed.size_bytes;
        let chunk_size = 4u64;
        let count = ((total + chunk_size - 1) / chunk_size) as u32;
        let mut via_offset = Vec::new();
        for i in 0..count {
            let len = chunk_len_at(total, chunk_size, i, count) as usize;
            let piece = read_file_chunk_at(&out, i as u64 * chunk_size, len).unwrap();
            assert_eq!(piece.len(), len);
            via_offset.extend_from_slice(&piece);
        }
        assert_eq!(via_offset, fs::read(&out).unwrap());
    }

    #[test]
    fn chunk_len_at_matches_last_partial() {
        assert_eq!(chunk_len_at(10, 4, 0, 3), 4);
        assert_eq!(chunk_len_at(10, 4, 1, 3), 4);
        assert_eq!(chunk_len_at(10, 4, 2, 3), 2);
        assert_eq!(chunk_len_at(8, 4, 1, 2), 4);
        assert_eq!(chunk_len_at(0, 4, 0, 0), 0);
    }
}
