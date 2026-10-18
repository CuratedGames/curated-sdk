//! End-to-end: pack content → chunked upload → register build on a depot.

use crate::client::{ApiClient, ApiError, RegisteredBuild};
use crate::pack::{pack_content_directory, read_file_chunks, PackError};
use std::fs;
use std::path::{Path, PathBuf};
use uuid::Uuid;

#[derive(Debug, Clone)]
pub struct UploadOptions {
    pub app_id: i64,
    pub content_root: PathBuf,
    pub version: String,
    pub depot_id: Option<Uuid>,
    pub depot_key: Option<String>,
    pub desc: Option<String>,
    pub filename: Option<String>,
    pub build_output: Option<PathBuf>,
    pub chunk_size: Option<i64>,
}

#[derive(Debug, Clone)]
pub struct UploadResult {
    pub build: RegisteredBuild,
    pub artifact_uri: String,
    pub artifact_sha256: String,
    pub depot_id: Uuid,
    pub zip_path: PathBuf,
    pub file_count: usize,
}

#[derive(Debug)]
pub enum UploadError {
    Api(ApiError),
    Pack(PackError),
    Message(String),
}

impl std::fmt::Display for UploadError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            UploadError::Api(e) => write!(f, "{e}"),
            UploadError::Pack(e) => write!(f, "{e}"),
            UploadError::Message(m) => write!(f, "{m}"),
        }
    }
}

impl std::error::Error for UploadError {}

impl From<ApiError> for UploadError {
    fn from(value: ApiError) -> Self {
        UploadError::Api(value)
    }
}

impl From<PackError> for UploadError {
    fn from(value: PackError) -> Self {
        UploadError::Pack(value)
    }
}

fn resolve_depot_id(client: &ApiClient, app_id: i64, opts: &UploadOptions) -> Result<Uuid, UploadError> {
    if let Some(id) = opts.depot_id {
        return Ok(id);
    }
    let depots = client.list_depots(app_id)?;
    if let Some(key) = opts.depot_key.as_deref() {
        let key = key.trim();
        let found = depots.iter().find(|d| d.depot_key.eq_ignore_ascii_case(key));
        return found
            .map(|d| d.id)
            .ok_or_else(|| UploadError::Message(format!("no depot with key '{key}' for app {app_id}")));
    }
    if depots.len() == 1 {
        return Ok(depots[0].id);
    }
    let content = depots.iter().find(|d| d.depot_key == "content");
    if let Some(d) = content {
        return Ok(d.id);
    }
    Err(UploadError::Message(
        "specify --depot-id or --depot-key (multiple depots on this app)".into(),
    ))
}

fn default_zip_path(opts: &UploadOptions) -> PathBuf {
    let filename = opts
        .filename
        .as_deref()
        .unwrap_or("depot.zip");
    if let Some(out) = &opts.build_output {
        out.join(filename)
    } else {
        opts.content_root
            .parent()
            .unwrap_or(Path::new("."))
            .join("output")
            .join(filename)
    }
}

/// Pack the content directory, upload chunks, and register the build.
pub fn run_upload(client: &ApiClient, opts: UploadOptions) -> Result<UploadResult, UploadError> {
    if opts.version.trim().is_empty() {
        return Err(UploadError::Message("version is required".into()));
    }
    // Ownership gate before packing (also enforced again inside list_depots / server).
    client.require_app_ownership(opts.app_id)?;
    let depot_id = resolve_depot_id(client, opts.app_id, &opts)?;
    let zip_path = default_zip_path(&opts);
    eprintln!(
        "Packing {} → {}",
        opts.content_root.display(),
        zip_path.display()
    );
    let packed = pack_content_directory(&opts.content_root, &zip_path)?;
    eprintln!(
        "Packed {} files ({} bytes, sha256 {}…)",
        packed.files.len(),
        packed.size_bytes,
        &packed.sha256[..12]
    );

    let filename = zip_path
        .file_name()
        .and_then(|s| s.to_str())
        .unwrap_or("depot.zip");

    let init = client.init_content_upload(
        opts.app_id,
        opts.version.trim(),
        packed.size_bytes as i64,
        &packed.sha256,
        Some(depot_id),
        Some(filename),
        opts.chunk_size,
    )?;

    if init.chunk_count as usize != init.chunks.len() {
        return Err(UploadError::Message(
            "server returned inconsistent chunk list".into(),
        ));
    }

    let chunk_size = init.chunk_size as usize;
    let file_chunks = read_file_chunks(&packed.zip_path, chunk_size)?;
    if file_chunks.len() != init.chunk_count as usize {
        return Err(UploadError::Message(format!(
            "local chunk count {} != server chunk count {}",
            file_chunks.len(),
            init.chunk_count
        )));
    }

    for (chunk_meta, bytes) in init.chunks.iter().zip(file_chunks.iter()) {
        eprintln!(
            "Uploading chunk {}/{} ({} bytes)",
            chunk_meta.index + 1,
            init.chunk_count,
            bytes.len()
        );
        client.put_chunk(&chunk_meta.url, bytes)?;
    }

    let completed = client.complete_content_upload(opts.app_id, init.upload_id)?;
    eprintln!("Upload complete: {}", completed.artifact_uri);

    let files: Vec<(String, String, u64)> = packed
        .files
        .iter()
        .map(|f| (f.path.clone(), f.sha256.clone(), f.size_bytes))
        .collect();

    let build = client.register_build(
        opts.app_id,
        opts.version.trim(),
        &completed.artifact_uri,
        &completed.artifact_sha256,
        completed.depot_id,
        opts.desc.as_deref(),
        &files,
    )?;

    eprintln!(
        "Registered build {} on depot {} (scan: {})",
        build.id,
        build
            .depot_key
            .as_deref()
            .unwrap_or(&completed.depot_id.to_string()),
        build.scan_status.as_deref().unwrap_or("pending")
    );

    // Keep zip in build_output for local inspection; nothing else to clean.
    let _ = fs::metadata(&zip_path);

    Ok(UploadResult {
        build,
        artifact_uri: completed.artifact_uri,
        artifact_sha256: completed.artifact_sha256,
        depot_id: completed.depot_id,
        zip_path,
        file_count: packed.files.len(),
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn zip_path_uses_build_output() {
        let opts = UploadOptions {
            app_id: 1,
            content_root: PathBuf::from("C:/game/content"),
            version: "1.0.0".into(),
            depot_id: None,
            depot_key: None,
            desc: None,
            filename: Some("win.zip".into()),
            build_output: Some(PathBuf::from("C:/game/output")),
            chunk_size: None,
        };
        assert_eq!(
            default_zip_path(&opts),
            PathBuf::from("C:/game/output/win.zip")
        );
    }
}
