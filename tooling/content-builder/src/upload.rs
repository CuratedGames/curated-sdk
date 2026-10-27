//! End-to-end: pack content → chunked upload → register build on a depot.

use crate::client::{ApiClient, ApiError, ChunkUrl, UploadLimits};
use crate::pack::{
    chunk_len_at, pack_content_directory, read_file_chunk_at, PackError,
};
use crate::throughput::{
    choose_chunk_size, choose_concurrency, BANDWIDTH_PROBE_BYTES, DEFAULT_CHUNK_SIZE,
    MAX_CHUNK_SIZE, MAX_CONCURRENCY, MIN_CHUNK_SIZE,
};
use std::fs;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::Mutex;

#[derive(Debug, Clone)]
pub struct UploadOptions {
    pub app_id: i64,
    pub content_root: PathBuf,
    pub version: String,
    /// Depot ID `<app_id>_<NN>`. `None`: the base depot `<app_id>_00`.
    pub depot_id: Option<String>,
    pub desc: Option<String>,
    pub filename: Option<String>,
    pub build_output: Option<PathBuf>,
    /// Explicit chunk size. `None`: measure bandwidth and pick within server limits.
    pub chunk_size: Option<i64>,
    /// Parallel chunk PUTs. `None`: derive from measured bandwidth (capped by server).
    pub concurrency: Option<usize>,
}

#[derive(Debug, Clone)]
pub struct UploadResult {
    pub build: crate::client::RegisteredBuild,
    pub artifact_uri: String,
    pub artifact_sha256: String,
    pub depot_id: String,
    pub zip_path: PathBuf,
    pub file_count: usize,
    pub chunk_size: i64,
    pub concurrency: usize,
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

/// Check a depot ID before any network call: `<app_id>_<NN>` (two-digit number) of the
/// app being uploaded. Returns the trimmed ID.
pub fn validate_depot_id(raw: &str, app_id: i64) -> Result<String, String> {
    let id = raw.trim();
    let well_formed = id.split_once('_').is_some_and(|(app, number)| {
        !app.is_empty()
            && app.bytes().all(|b| b.is_ascii_digit())
            && number.len() == 2
            && number.bytes().all(|b| b.is_ascii_digit())
    });
    if !well_formed {
        return Err(format!(
            "invalid depot id '{id}': expected <app_id>_<NN>, for example {app_id}_00 \
(the base depot) or {app_id}_01. Depot keys such as 'content' and UUIDs are no longer \
accepted; run `curated-content-builder list-depots --app-id {app_id}` to see the depot IDs"
        ));
    }
    let app_part = id.split('_').next().unwrap_or_default();
    if app_part != app_id.to_string() {
        return Err(format!(
            "depot id '{id}' belongs to app {app_part}, not app {app_id}: \
depot IDs of app {app_id} look like {app_id}_00"
        ));
    }
    Ok(id.to_string())
}

/// Depot to upload to: the given depot ID, else the base depot `<app_id>_00`.
fn resolve_depot_id(app_id: i64, opts: &UploadOptions) -> Result<String, UploadError> {
    match opts
        .depot_id
        .as_deref()
        .map(str::trim)
        .filter(|s| !s.is_empty())
    {
        Some(raw) => validate_depot_id(raw, app_id).map_err(UploadError::Message),
        None => Ok(format!("{app_id}_00")),
    }
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

fn fallback_limits() -> UploadLimits {
    UploadLimits {
        min_chunk_size: MIN_CHUNK_SIZE,
        max_chunk_size: MAX_CHUNK_SIZE,
        default_chunk_size: DEFAULT_CHUNK_SIZE,
        max_upload_bytes: 5_368_709_120,
        max_concurrent_chunks: MAX_CONCURRENCY as u32,
        max_bandwidth_probe_bytes: Some(1_048_576),
    }
}

/// Measure upload bandwidth (when possible) and resolve chunk size + concurrency.
pub fn resolve_throughput(
    client: &ApiClient,
    app_id: i64,
    opts: &UploadOptions,
) -> Result<(i64, usize, UploadLimits, Option<f64>), UploadError> {
    let mut limits = fallback_limits();
    let mut measured_bps: Option<f64> = None;

    let probe_len = BANDWIDTH_PROBE_BYTES
        .min(
            limits
                .max_bandwidth_probe_bytes
                .unwrap_or(1_048_576)
                .max(1) as usize,
        )
        .max(1);
    let probe_body = vec![0u8; probe_len];

    match client.bandwidth_probe(app_id, &probe_body) {
        Ok((elapsed, resp)) => {
            if let Some(l) = resp.limits {
                limits = l;
            }
            let secs = elapsed.as_secs_f64().max(1e-6);
            measured_bps = Some(resp.bytes.max(probe_len) as f64 / secs);
            eprintln!(
                "Bandwidth probe: {} bytes in {:.2}s ({:.1} MiB/s)",
                resp.bytes.max(probe_len),
                secs,
                measured_bps.unwrap_or(0.0) / (1024.0 * 1024.0)
            );
        }
        Err(e) => {
            eprintln!("Bandwidth probe unavailable ({e}); falling back to limits/defaults");
            if let Ok(l) = client.content_upload_limits(app_id) {
                limits = l;
            }
        }
    }

    let chunk_size = opts.chunk_size.unwrap_or_else(|| {
        choose_chunk_size(
            measured_bps,
            limits.min_chunk_size,
            limits.max_chunk_size,
            limits.default_chunk_size,
        )
    });
    if chunk_size < limits.min_chunk_size || chunk_size > limits.max_chunk_size {
        return Err(UploadError::Message(format!(
            "chunk size {chunk_size} out of server range {}..{}",
            limits.min_chunk_size, limits.max_chunk_size
        )));
    }

    let max_conc = (limits.max_concurrent_chunks as usize).clamp(1, MAX_CONCURRENCY);
    let concurrency = opts
        .concurrency
        .map(|n| n.clamp(1, max_conc))
        .unwrap_or_else(|| choose_concurrency(measured_bps, chunk_size, max_conc));

    Ok((chunk_size, concurrency, limits, measured_bps))
}

fn is_chunk_size_rejected(err: &ApiError) -> bool {
    match err {
        ApiError::Status { message, .. } => message.to_ascii_lowercase().contains("chunk_size"),
        _ => false,
    }
}

/// Upload all chunks with up to `concurrency` parallel PUTs. Reads each chunk from disk
/// by offset so large depots are not held entirely in memory.
pub fn put_chunks_parallel(
    client: &ApiClient,
    zip_path: &Path,
    chunks: &[ChunkUrl],
    chunk_size: usize,
    total_bytes: u64,
    concurrency: usize,
) -> Result<(), UploadError> {
    if chunks.is_empty() {
        return Ok(());
    }
    let chunk_count = chunks.len() as u32;
    let concurrency = concurrency.clamp(1, MAX_CONCURRENCY).min(chunks.len());
    let next = AtomicUsize::new(0);
    let done = AtomicUsize::new(0);
    let first_error: Mutex<Option<UploadError>> = Mutex::new(None);

    std::thread::scope(|scope| {
        for _ in 0..concurrency {
            scope.spawn(|| {
                loop {
                    if first_error.lock().ok().and_then(|g| g.as_ref().map(|_| ())).is_some() {
                        break;
                    }
                    let i = next.fetch_add(1, Ordering::SeqCst);
                    if i >= chunks.len() {
                        break;
                    }
                    let meta = &chunks[i];
                    let expected = chunk_len_at(
                        total_bytes,
                        chunk_size as u64,
                        meta.index,
                        chunk_count,
                    ) as usize;
                    let offset = meta.index as u64 * chunk_size as u64;
                    let bytes = match read_file_chunk_at(zip_path, offset, expected) {
                        Ok(b) => b,
                        Err(e) => {
                            if let Ok(mut g) = first_error.lock() {
                                if g.is_none() {
                                    *g = Some(UploadError::Pack(e));
                                }
                            }
                            break;
                        }
                    };
                    if bytes.len() != expected {
                        if let Ok(mut g) = first_error.lock() {
                            if g.is_none() {
                                *g = Some(UploadError::Message(format!(
                                    "chunk {}: read {} bytes, expected {expected}",
                                    meta.index,
                                    bytes.len()
                                )));
                            }
                        }
                        break;
                    }
                    if let Err(e) = client.put_chunk(&meta.url, &bytes) {
                        if let Ok(mut g) = first_error.lock() {
                            if g.is_none() {
                                *g = Some(UploadError::Api(e));
                            }
                        }
                        break;
                    }
                    let finished = done.fetch_add(1, Ordering::SeqCst) + 1;
                    eprintln!(
                        "Uploaded chunk {finished}/{chunk_count} ({} bytes)",
                        bytes.len()
                    );
                }
            });
        }
    });

    if let Some(err) = first_error.into_inner().ok().and_then(|e| e) {
        return Err(err);
    }
    let finished = done.load(Ordering::SeqCst);
    if finished != chunks.len() {
        return Err(UploadError::Message(format!(
            "uploaded {finished} of {} chunks",
            chunks.len()
        )));
    }
    Ok(())
}

/// Pack the content directory, upload chunks, and register the build.
pub fn run_upload(client: &ApiClient, opts: UploadOptions) -> Result<UploadResult, UploadError> {
    if opts.version.trim().is_empty() {
        return Err(UploadError::Message("version is required".into()));
    }
    let depot_id = resolve_depot_id(opts.app_id, &opts)?;
    // Ownership gate before packing (the server checks it again).
    client.require_app_ownership(opts.app_id)?;
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

    let (mut chunk_size, concurrency, _limits, _bps) =
        resolve_throughput(client, opts.app_id, &opts)?;
    eprintln!("Upload plan: chunk_size={chunk_size} concurrency={concurrency}");

    let mut init = match client.init_content_upload(
        opts.app_id,
        opts.version.trim(),
        packed.size_bytes as i64,
        &packed.sha256,
        Some(depot_id.as_str()),
        Some(filename),
        Some(chunk_size),
    ) {
        Ok(v) => v,
        Err(e) if opts.chunk_size.is_none() && is_chunk_size_rejected(&e) => {
            // Older API max (8 MiB) or body-limit mismatch: retry with legacy 1 MiB.
            eprintln!("Server rejected chunk size {chunk_size} ({e}); retrying with 1 MiB");
            chunk_size = 1_048_576;
            client.init_content_upload(
                opts.app_id,
                opts.version.trim(),
                packed.size_bytes as i64,
                &packed.sha256,
                Some(depot_id.as_str()),
                Some(filename),
                Some(chunk_size),
            )?
        }
        Err(e) => return Err(e.into()),
    };

    // Prefer the size the server actually accepted.
    chunk_size = init.chunk_size;

    if init.chunk_count as usize != init.chunks.len() {
        return Err(UploadError::Message(
            "server returned inconsistent chunk list".into(),
        ));
    }

    // Sort by index so offset math matches server order even if URLs arrive shuffled.
    init.chunks.sort_by_key(|c| c.index);

    put_chunks_parallel(
        client,
        &packed.zip_path,
        &init.chunks,
        chunk_size as usize,
        packed.size_bytes,
        concurrency,
    )?;

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
        &completed.depot_id,
        opts.desc.as_deref(),
        &files,
    )?;

    eprintln!(
        "Registered build {} on depot {} (scan: {})",
        build.id,
        build.depot_id.as_deref().unwrap_or(&completed.depot_id),
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
        chunk_size,
        concurrency,
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
            desc: None,
            filename: Some("win.zip".into()),
            build_output: Some(PathBuf::from("C:/game/output")),
            chunk_size: None,
            concurrency: None,
        };
        assert_eq!(
            default_zip_path(&opts),
            PathBuf::from("C:/game/output/win.zip")
        );
        assert_eq!(resolve_depot_id(1, &opts).unwrap(), "1_00");
    }

    #[test]
    fn depot_ids_are_app_and_two_digits() {
        assert_eq!(validate_depot_id("1000_00", 1000).unwrap(), "1000_00");
        assert_eq!(validate_depot_id(" 1000_07 ", 1000).unwrap(), "1000_07");
        for bad in [
            "content",
            "",
            "1000",
            "1000_7",
            "1000_100",
            "_00",
            "abc_00",
            "1000-00",
            "00000000-0000-0000-0000-000000000000",
        ] {
            let e = validate_depot_id(bad, 1000).unwrap_err();
            assert!(e.contains("<app_id>_<NN>"), "{bad}: {e}");
            assert!(e.contains("1000_00"), "{bad}: {e}");
        }
        let other = validate_depot_id("2000_00", 1000).unwrap_err();
        assert!(other.contains("not app 1000"), "{other}");
    }

    #[test]
    fn chunk_size_rejection_detects_message() {
        assert!(is_chunk_size_rejected(&ApiError::Status {
            status: 400,
            message: "chunk_size out of range".into(),
        }));
        assert!(!is_chunk_size_rejected(&ApiError::Status {
            status: 403,
            message: "not game owner".into(),
        }));
    }
}
