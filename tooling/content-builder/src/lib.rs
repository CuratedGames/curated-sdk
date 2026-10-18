//! Curated content builder: pack a content directory, upload chunks to a depot,
//! and register the build. Works for draft / unreleased apps (owner auth only).

mod auth;
mod client;
mod config;
mod pack;
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
    file_sha256_hex, pack_content_directory, relative_posix_path, walk_manifest_files,
    ManifestEntry, PackError, PackedArtifact,
};
pub use upload::{run_upload, UploadError, UploadOptions, UploadResult};

/// Default public API origin when neither flag nor env is set.
pub const DEFAULT_API_BASE: &str = "https://api.curated.games";
