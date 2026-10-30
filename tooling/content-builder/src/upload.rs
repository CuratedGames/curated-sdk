//! End-to-end: stage CAS blobs → upload missing objects → register build on a depot.

use crate::client::{ApiClient, ApiError, CasLimits, CasObjectRef, ChunkUrl, RegisterManifestFile, UploadLimits};
use crate::pack::{
    chunk_len_at, manifest_content_sha256, prepare_cas_entries, read_file_chunk_at, PackError,
    PreparedCasFile,
};
use crate::throughput::{
    choose_chunk_size, choose_concurrency, BANDWIDTH_PROBE_BYTES, DEFAULT_CHUNK_SIZE,
    DEFAULT_CONCURRENCY, MAX_CHUNK_SIZE, MAX_CONCURRENCY, MIN_CHUNK_SIZE,
};
use std::collections::{HashMap, HashSet};
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
    pub manifest_sha256: String,
    pub file_count: usize,
    pub uploaded_objects: usize,
    pub reused_objects: usize,
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

fn staging_dir(opts: &UploadOptions) -> PathBuf {
    if let Some(out) = &opts.build_output {
        out.join(".cas-staging")
    } else {
        opts.content_root
            .parent()
            .unwrap_or(Path::new("."))
            .join("output")
            .join(".cas-staging")
    }
}

fn fallback_cas_limits() -> CasLimits {
    CasLimits {
        max_objects_per_missing_request: 2000,
        max_objects_per_upload_url_request: 100,
        max_object_bytes: 5_368_709_120,
        max_concurrent_puts: MAX_CONCURRENCY as u32,
        presign_ttl_seconds: 3600,
        codecs: vec!["zstd".into(), "identity".into()],
    }
}

/// Measure upload bandwidth (when possible) and resolve parallel PUT count for CAS.
pub fn resolve_cas_concurrency(
    client: &ApiClient,
    app_id: i64,
    opts: &UploadOptions,
) -> Result<(usize, CasLimits, Option<f64>), UploadError> {
    let mut limits = fallback_cas_limits();
    let mut measured_bps: Option<f64> = None;

    let probe_len = BANDWIDTH_PROBE_BYTES.max(1);
    let probe_body = vec![0u8; probe_len];

    match client.bandwidth_probe(app_id, &probe_body) {
        Ok((elapsed, _resp)) => {
            let secs = elapsed.as_secs_f64().max(1e-6);
            measured_bps = Some(probe_body.len() as f64 / secs);
            eprintln!(
                "Bandwidth probe: {} bytes in {:.2}s ({:.1} MiB/s)",
                probe_body.len(),
                secs,
                measured_bps.unwrap_or(0.0) / (1024.0 * 1024.0)
            );
        }
        Err(e) => {
            eprintln!("Bandwidth probe unavailable ({e}); falling back to CAS limits/defaults");
        }
    }

    if let Ok(l) = client.cas_limits(app_id) {
        limits = l;
    }

    let max_conc = (limits.max_concurrent_puts as usize).clamp(1, MAX_CONCURRENCY);
    // Reuse chunk-size field as a hint for target object size when tuning concurrency.
    let hint_bytes = opts.chunk_size.unwrap_or(8_388_608).max(1);
    let concurrency = opts
        .concurrency
        .map(|n| n.clamp(1, max_conc))
        .unwrap_or_else(|| {
            choose_concurrency(measured_bps, hint_bytes, max_conc).max(DEFAULT_CONCURRENCY.min(max_conc))
        })
        .clamp(1, max_conc);

    Ok((concurrency, limits, measured_bps))
}

fn cas_content_type(codec: &str) -> &'static str {
    if codec == "zstd" {
        "application/zstd"
    } else {
        "application/octet-stream"
    }
}

#[derive(Debug, Clone)]
pub struct CasPutJob {
    pub object: CasObjectRef,
    pub url: String,
    pub compressed_size_bytes: u64,
}

fn batch_cas_missing(
    client: &ApiClient,
    app_id: i64,
    objects: &[CasObjectRef],
    limits: &CasLimits,
) -> Result<Vec<CasObjectRef>, UploadError> {
    let max = limits.max_objects_per_missing_request.max(1) as usize;
    let mut missing = Vec::new();
    for chunk in objects.chunks(max) {
        let part = client.cas_missing(app_id, chunk)?;
        missing.extend(part);
    }
    Ok(missing)
}

/// Upload CAS blobs with up to `concurrency` parallel PUTs.
pub fn put_cas_objects_parallel(
    client: &ApiClient,
    jobs: &[CasPutJob],
    blob_paths: &HashMap<(String, String), PathBuf>,
    concurrency: usize,
) -> Result<(), UploadError> {
    if jobs.is_empty() {
        return Ok(());
    }
    let concurrency = concurrency.clamp(1, MAX_CONCURRENCY).min(jobs.len());
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
                    if i >= jobs.len() {
                        break;
                    }
                    let job = &jobs[i];
                    let key = (job.object.sha256.clone(), job.object.codec.clone());
                    let blob_path = match blob_paths.get(&key) {
                        Some(p) => p,
                        None => {
                            if let Ok(mut g) = first_error.lock() {
                                if g.is_none() {
                                    *g = Some(UploadError::Message(format!(
                                        "missing staged blob for {} ({})",
                                        job.object.sha256, job.object.codec
                                    )));
                                }
                            }
                            break;
                        }
                    };
                    let bytes = match fs::read(blob_path) {
                        Ok(b) => b,
                        Err(e) => {
                            if let Ok(mut g) = first_error.lock() {
                                if g.is_none() {
                                    *g = Some(UploadError::Pack(PackError::Io(e)));
                                }
                            }
                            break;
                        }
                    };
                    if bytes.len() as u64 != job.compressed_size_bytes {
                        if let Ok(mut g) = first_error.lock() {
                            if g.is_none() {
                                *g = Some(UploadError::Message(format!(
                                    "blob {} ({}): read {} bytes, expected {}",
                                    job.object.sha256,
                                    job.object.codec,
                                    bytes.len(),
                                    job.compressed_size_bytes
                                )));
                            }
                        }
                        break;
                    }
                    if let Err(e) = client.put_cas_object(
                        &job.url,
                        &bytes,
                        cas_content_type(&job.object.codec),
                    ) {
                        if let Ok(mut g) = first_error.lock() {
                            if g.is_none() {
                                *g = Some(UploadError::Api(e));
                            }
                        }
                        break;
                    }
                    let finished = done.fetch_add(1, Ordering::SeqCst) + 1;
                    eprintln!(
                        "Uploaded CAS object {finished}/{} ({} bytes, {})",
                        jobs.len(),
                        bytes.len(),
                        job.object.codec
                    );
                }
            });
        }
    });

    if let Some(err) = first_error.into_inner().ok().and_then(|e| e) {
        return Err(err);
    }
    let finished = done.load(Ordering::SeqCst);
    if finished != jobs.len() {
        return Err(UploadError::Message(format!(
            "uploaded {finished} of {} CAS objects",
            jobs.len()
        )));
    }
    Ok(())
}

fn unique_cas_objects(prepared: &[PreparedCasFile]) -> Vec<(CasObjectRef, u64, PathBuf)> {
    let mut seen = HashSet::new();
    let mut out = Vec::new();
    for p in prepared {
        let key = (p.entry.sha256.clone(), p.entry.codec.clone());
        if seen.insert(key) {
            out.push((
                CasObjectRef {
                    sha256: p.entry.sha256.clone(),
                    codec: p.entry.codec.clone(),
                },
                p.entry.compressed_size_bytes,
                p.blob_path.clone(),
            ));
        }
    }
    out
}

/// Stage content files, upload missing CAS objects, and register the build.
pub fn run_upload(client: &ApiClient, opts: UploadOptions) -> Result<UploadResult, UploadError> {
    if opts.version.trim().is_empty() {
        return Err(UploadError::Message("version is required".into()));
    }
    let depot_id = resolve_depot_id(opts.app_id, &opts)?;
    client.require_app_ownership(opts.app_id)?;

    let stage = staging_dir(&opts);
    eprintln!(
        "Staging CAS blobs from {} → {}",
        opts.content_root.display(),
        stage.display()
    );
    let prepared = prepare_cas_entries(&opts.content_root, &stage)?;
    let manifest_sha256 = manifest_content_sha256(
        &prepared.iter().map(|p| p.entry.clone()).collect::<Vec<_>>(),
    );
    eprintln!(
        "Prepared {} files (manifest sha256 {}…)",
        prepared.len(),
        &manifest_sha256[..12.min(manifest_sha256.len())]
    );

    let unique = unique_cas_objects(&prepared);
    let all_refs: Vec<CasObjectRef> = unique
        .iter()
        .map(|(o, _, _)| CasObjectRef {
            sha256: o.sha256.clone(),
            codec: o.codec.clone(),
        })
        .collect();

    let (concurrency, limits, _bps) = resolve_cas_concurrency(client, opts.app_id, &opts)?;
    eprintln!("Upload plan: concurrency={concurrency}");

    let missing = batch_cas_missing(client, opts.app_id, &all_refs, &limits)?;
    let missing_set: HashSet<(String, String)> = missing
        .iter()
        .map(|o| (o.sha256.clone(), o.codec.clone()))
        .collect();
    let reused_objects = all_refs.len().saturating_sub(missing.len());

    let to_upload: Vec<(CasObjectRef, u64)> = unique
        .iter()
        .filter(|(o, _, _)| missing_set.contains(&(o.sha256.clone(), o.codec.clone())))
        .map(|(o, size, _)| (o.clone(), *size))
        .collect();

    let blob_paths: HashMap<(String, String), PathBuf> = unique
        .iter()
        .map(|(o, _, path)| ((o.sha256.clone(), o.codec.clone()), path.clone()))
        .collect();

    let mut jobs = Vec::new();
    if !to_upload.is_empty() {
        let max = limits.max_objects_per_upload_url_request.max(1) as usize;
        for chunk in to_upload.chunks(max) {
            let uploads = client.cas_upload_urls(opts.app_id, chunk)?;
            for entry in uploads {
                let compressed_size_bytes = chunk
                    .iter()
                    .find(|(o, _)| o.sha256 == entry.sha256 && o.codec == entry.codec)
                    .map(|(_, s)| *s)
                    .ok_or_else(|| {
                        UploadError::Message(
                            "server returned upload URL for unknown object".into(),
                        )
                    })?;
                jobs.push(CasPutJob {
                    object: CasObjectRef {
                        sha256: entry.sha256,
                        codec: entry.codec,
                    },
                    url: entry.url,
                    compressed_size_bytes,
                });
            }
        }
        put_cas_objects_parallel(client, &jobs, &blob_paths, concurrency)?;
    }

    let register_files: Vec<RegisterManifestFile> = prepared
        .iter()
        .map(|p| RegisterManifestFile {
            path: p.entry.path.clone(),
            sha256: p.entry.sha256.clone(),
            size_bytes: p.entry.size_bytes,
            codec: p.entry.codec.clone(),
            compressed_size_bytes: p.entry.compressed_size_bytes,
        })
        .collect();

    let build = client.register_build(
        opts.app_id,
        opts.version.trim(),
        &manifest_sha256,
        &depot_id,
        opts.desc.as_deref(),
        &register_files,
    )?;

    let artifact_uri = build
        .artifact_uri
        .clone()
        .unwrap_or_default();
    let artifact_sha256 = build
        .artifact_sha256
        .clone()
        .unwrap_or_else(|| manifest_sha256.clone());

    eprintln!(
        "Registered build {} on depot {} (scan: {})",
        build.id,
        build.depot_id.as_deref().unwrap_or(&depot_id),
        build.scan_status.as_deref().unwrap_or("pending")
    );

    Ok(UploadResult {
        build,
        artifact_uri,
        artifact_sha256,
        depot_id,
        manifest_sha256,
        file_count: prepared.len(),
        uploaded_objects: jobs.len(),
        reused_objects,
        concurrency,
    })
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

/// Measure upload bandwidth (when possible) and resolve chunk size + concurrency (legacy zip helpers).
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
        }
        Err(_) => {
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

    let max_conc = (limits.max_concurrent_chunks as usize).clamp(1, MAX_CONCURRENCY);
    let concurrency = opts
        .concurrency
        .map(|n| n.clamp(1, max_conc))
        .unwrap_or_else(|| choose_concurrency(measured_bps, chunk_size, max_conc));

    Ok((chunk_size, concurrency, limits, measured_bps))
}

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
                    let _finished = done.fetch_add(1, Ordering::SeqCst) + 1;
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn staging_dir_uses_build_output() {
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
            staging_dir(&opts),
            PathBuf::from("C:/game/output/.cas-staging")
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
    fn unique_cas_objects_dedupes_by_hash_and_codec() {
        let a = PreparedCasFile {
            entry: crate::pack::ManifestEntry {
                path: "a".into(),
                sha256: "aa".repeat(32),
                size_bytes: 1,
                codec: "zstd".into(),
                compressed_size_bytes: 1,
            },
            blob_path: PathBuf::from("/tmp/a"),
        };
        let b = PreparedCasFile {
            entry: crate::pack::ManifestEntry {
                path: "b".into(),
                sha256: "aa".repeat(32),
                size_bytes: 1,
                codec: "zstd".into(),
                compressed_size_bytes: 1,
            },
            blob_path: PathBuf::from("/tmp/a"),
        };
        let u = unique_cas_objects(&[a, b]);
        assert_eq!(u.len(), 1);
    }
}
