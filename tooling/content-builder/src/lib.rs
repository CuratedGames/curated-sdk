//! Curated content builder: stage CAS blobs, upload to a depot,
//! and register the build. Works for draft / unreleased apps (owner auth only).

mod auth;
mod client;
mod config;
mod pack;
mod throughput;
mod upload;

pub use auth::{
    clear_credentials, credentials_path, load_credentials, login_and_store, save_credentials,
    Credentials, LoginError,
};
pub use client::{
    owns_app, ApiClient, ApiError, DepotRow, InitUploadResponse, OwnedGame, RegisteredBuild,
};
pub use config::{load_build_script, BuildScript, DepotMapping, ScriptError};
pub use pack::{
    compress_file_zstd, file_sha256_hex, manifest_content_sha256, pack_content_directory,
    prepare_cas_entries, relative_posix_path, walk_manifest_files, ManifestEntry, PackError,
    PackedArtifact, PreparedCasFile,
};
pub use throughput::{
    choose_chunk_size, choose_concurrency, DEFAULT_CHUNK_SIZE, DEFAULT_CONCURRENCY, MAX_CHUNK_SIZE,
    MAX_CONCURRENCY, MIN_CHUNK_SIZE,
};
pub use upload::{
    put_cas_objects_parallel, put_chunks_parallel, resolve_cas_concurrency, resolve_throughput,
    run_upload, validate_depot_id, CasPutJob, UploadError, UploadOptions, UploadResult,
};

/// Default public API origin when neither flag nor env is set.
pub const DEFAULT_API_BASE: &str = "https://api.curated.games";
