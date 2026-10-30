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
    pub codec: String,
    pub compressed_size_bytes: u64,
}

#[derive(Debug, Clone)]
pub struct PreparedCasFile {
    pub entry: ManifestEntry,
    pub blob_path: PathBuf,
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
            codec: String::new(),
            compressed_size_bytes: 0,
        });
    }
    files.sort_by(|a, b| a.path.cmp(&b.path));
    Ok(files)
}

/// Default zstd compression level for depot blobs.
pub const DEFAULT_ZSTD_LEVEL: i32 = 3;

/// Compress `src` into `dst` with zstd. Returns compressed byte length.
pub fn compress_file_zstd(src: &Path, dst: &Path, level: i32) -> Result<u64, PackError> {
    if let Some(parent) = dst.parent() {
        fs::create_dir_all(parent)?;
    }
    let mut input = File::open(src)?;
    let output = File::create(dst)?;
    let mut encoder = zstd::stream::write::Encoder::new(output, level)?;
    io::copy(&mut input, &mut encoder)?;
    encoder.finish()?;
    Ok(fs::metadata(dst)?.len())
}

/// SHA-256 hex of the canonical manifest digest (must match API `manifest_content_sha256`).
pub fn manifest_content_sha256(files: &[ManifestEntry]) -> String {
    let mut sorted: Vec<&ManifestEntry> = files.iter().collect();
    sorted.sort_by(|a, b| a.path.cmp(&b.path));
    let mut hasher = Sha256::new();
    for f in sorted {
        let line = format!(
            "{}\n{}\n{}\n{}\n{}\n",
            f.path, f.sha256, f.size_bytes, f.codec, f.compressed_size_bytes
        );
        hasher.update(line.as_bytes());
    }
    hex::encode(hasher.finalize())
}

/// Walk content, hash each file, stage zstd or identity blobs under `staging_dir`.
pub fn prepare_cas_entries(
    content_root: &Path,
    staging_dir: &Path,
) -> Result<Vec<PreparedCasFile>, PackError> {
    fs::create_dir_all(staging_dir)?;
    let walked = walk_manifest_files(content_root)?;
    if walked.is_empty() {
        return Err(PackError::Message(
            "content directory has no files to upload".into(),
        ));
    }
    let mut prepared = Vec::with_capacity(walked.len());
    for partial in walked {
        let abs = content_root.join(partial.path.replace('/', std::path::MAIN_SEPARATOR_STR));
        let zstd_blob = staging_dir.join(format!("{}.zst", partial.sha256));
        let compressed_len = compress_file_zstd(&abs, &zstd_blob, DEFAULT_ZSTD_LEVEL)?;
        let threshold = ((partial.size_bytes as f64) * 0.95).floor() as u64;
        let (codec, blob_path, compressed_size_bytes) = if compressed_len < threshold {
            (
                "zstd".to_string(),
                zstd_blob,
                compressed_len,
            )
        } else {
            let _ = fs::remove_file(&zstd_blob);
            let identity_blob = staging_dir.join(format!("{}.raw", partial.sha256));
            fs::copy(&abs, &identity_blob)?;
            (
                "identity".to_string(),
                identity_blob,
                partial.size_bytes,
            )
        };
        prepared.push(PreparedCasFile {
            entry: ManifestEntry {
                path: partial.path,
                sha256: partial.sha256,
                size_bytes: partial.size_bytes,
                codec,
                compressed_size_bytes,
            },
            blob_path,
        });
    }
    Ok(prepared)
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
        assert_eq!(packed.files[0].codec, "");
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

    #[test]
    fn manifest_content_sha256_matches_canonical_lines() {
        let files = vec![
            ManifestEntry {
                path: "b.txt".into(),
                sha256: "bb".repeat(32),
                size_bytes: 2,
                codec: "zstd".into(),
                compressed_size_bytes: 1,
            },
            ManifestEntry {
                path: "a.txt".into(),
                sha256: "aa".repeat(32),
                size_bytes: 1,
                codec: "identity".into(),
                compressed_size_bytes: 1,
            },
        ];
        let digest = manifest_content_sha256(&files);
        assert_eq!(digest.len(), 64);
        // Sorted by path: a.txt before b.txt
        let mut hasher = Sha256::new();
        hasher.update(b"a.txt\n");
        hasher.update(format!("{}\n", "aa".repeat(32)).as_bytes());
        hasher.update(b"1\nidentity\n1\n");
        hasher.update(b"b.txt\n");
        hasher.update(format!("{}\n", "bb".repeat(32)).as_bytes());
        hasher.update(b"2\nzstd\n1\n");
        assert_eq!(digest, hex::encode(hasher.finalize()));
    }

    #[test]
    fn prepare_cas_entries_stages_blobs() {
        let dir = tempdir().unwrap();
        let content = dir.path().join("content");
        fs::create_dir_all(content.join("data")).unwrap();
        let mut f = File::create(content.join("data").join("payload.bin")).unwrap();
        f.write_all(b"repeat-repeat-repeat").unwrap();
        let staging = dir.path().join("staging");
        let prepared = prepare_cas_entries(&content, &staging).unwrap();
        assert_eq!(prepared.len(), 1);
        assert_eq!(prepared[0].entry.path, "data/payload.bin");
        assert!(prepared[0].blob_path.exists());
        assert!(!prepared[0].entry.codec.is_empty());
        assert!(prepared[0].entry.compressed_size_bytes > 0);
        let digest = manifest_content_sha256(std::slice::from_ref(&prepared[0].entry));
        assert_eq!(digest.len(), 64);
    }
}
