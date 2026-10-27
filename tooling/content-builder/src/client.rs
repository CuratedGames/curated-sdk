//! Thin HTTP client for content-builder ↔ Curated API.

use reqwest::blocking::{Client, Response};
use reqwest::header::{AUTHORIZATION, CONTENT_TYPE};
use reqwest::StatusCode;
use serde::{Deserialize, Serialize};
use serde_json::Value;
use std::time::Duration;
use uuid::Uuid;

#[derive(Debug)]
pub enum ApiError {
    Http(reqwest::Error),
    Status { status: u16, message: String },
    Message(String),
}

impl std::fmt::Display for ApiError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            ApiError::Http(e) => write!(f, "{e}"),
            ApiError::Status { status, message } => write!(f, "HTTP {status}: {message}"),
            ApiError::Message(m) => write!(f, "{m}"),
        }
    }
}

impl std::error::Error for ApiError {}

impl From<reqwest::Error> for ApiError {
    fn from(value: reqwest::Error) -> Self {
        ApiError::Http(value)
    }
}

#[derive(Debug, Clone)]
pub struct ApiClient {
    base: String,
    token: Option<String>,
    http: Client,
}

#[derive(Debug, Deserialize)]
pub struct AuthResponse {
    pub user_id: String,
    pub token: String,
}

#[derive(Debug, Clone, Deserialize)]
pub struct OwnedGame {
    pub app_id: i64,
    #[serde(default)]
    pub title: Option<String>,
    #[serde(default)]
    pub id: Option<Uuid>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct DepotRow {
    /// Depot ID `<app_id>_<NN>`.
    pub depot_id: String,
    #[serde(default)]
    pub depot_number: Option<i16>,
    pub name: String,
    #[serde(default)]
    pub os: Option<String>,
    #[serde(default)]
    pub arch: Option<String>,
    #[serde(default)]
    pub language: Option<String>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct ChunkUrl {
    pub index: u32,
    pub url: String,
}

#[derive(Debug, Clone, Deserialize)]
pub struct InitUploadResponse {
    pub upload_id: Uuid,
    pub depot_id: String,
    pub version: String,
    pub artifact_uri: String,
    pub artifact_sha256: String,
    pub total_bytes: i64,
    pub chunk_size: i64,
    pub chunk_count: u32,
    pub chunks: Vec<ChunkUrl>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct CompleteUploadResponse {
    pub artifact_uri: String,
    pub artifact_sha256: String,
    pub depot_id: String,
    pub version: String,
    pub total_bytes: i64,
}

#[derive(Debug, Clone, Deserialize)]
pub struct RegisteredBuild {
    pub id: Uuid,
    pub version: String,
    #[serde(default)]
    pub artifact_uri: Option<String>,
    #[serde(default)]
    pub artifact_sha256: Option<String>,
    #[serde(default)]
    pub depot_id: Option<String>,
    #[serde(default)]
    pub scan_status: Option<String>,
}

#[derive(Debug, Serialize)]
struct LoginBody<'a> {
    login: &'a str,
    password: &'a str,
}

#[derive(Debug, Serialize)]
struct InitBody<'a> {
    version: &'a str,
    total_bytes: i64,
    artifact_sha256: &'a str,
    #[serde(skip_serializing_if = "Option::is_none")]
    depot_id: Option<&'a str>,
    #[serde(skip_serializing_if = "Option::is_none")]
    filename: Option<&'a str>,
    #[serde(skip_serializing_if = "Option::is_none")]
    chunk_size: Option<i64>,
}

#[derive(Debug, Serialize)]
struct ManifestFileBody<'a> {
    path: &'a str,
    sha256: &'a str,
    size_bytes: u64,
}

#[derive(Debug, Serialize)]
struct CreateBuildBody<'a> {
    version: &'a str,
    artifact_uri: &'a str,
    artifact_sha256: &'a str,
    #[serde(skip_serializing_if = "Option::is_none")]
    changelog: Option<&'a str>,
    depot_id: &'a str,
    files: Vec<ManifestFileBody<'a>>,
}

/// True when `app_id` appears in the developer's owned app list.
pub fn owns_app(owned: &[OwnedGame], app_id: i64) -> bool {
    owned.iter().any(|g| g.app_id == app_id)
}

impl ApiClient {
    pub fn new(api_base: &str, token: Option<&str>) -> Result<Self, ApiError> {
        let http = Client::builder()
            .timeout(Duration::from_secs(600))
            .build()?;
        Ok(Self {
            base: api_base.trim_end_matches('/').to_string(),
            token: token.map(|t| t.to_string()),
            http,
        })
    }

    fn url(&self, path: &str) -> String {
        format!("{}{}", self.base, path)
    }

    fn bearer(&self) -> Result<String, ApiError> {
        self.token
            .as_ref()
            .map(|t| format!("Bearer {t}"))
            .ok_or_else(|| {
                ApiError::Message(
                    "not signed in: run `curated-content-builder login` first".into(),
                )
            })
    }

    fn map_error(resp: Response) -> ApiError {
        let status = resp.status().as_u16();
        let body = resp.text().unwrap_or_default();
        let message = serde_json::from_str::<Value>(&body)
            .ok()
            .and_then(|v| {
                v.get("error")
                    .and_then(|e| e.as_str())
                    .map(|s| s.to_string())
            })
            .unwrap_or(body);
        let message = if status == 403
            && (message.is_empty()
                || message.contains("not game owner")
                || message.eq_ignore_ascii_case("forbidden"))
        {
            "not game owner: uploads and depot listing are limited to apps you own".into()
        } else {
            message
        };
        ApiError::Status { status, message }
    }

    pub fn login(&self, login: &str, password: &str) -> Result<AuthResponse, ApiError> {
        let resp = self
            .http
            .post(self.url("/auth/login"))
            .json(&LoginBody { login, password })
            .send()?;
        if !resp.status().is_success() {
            return Err(Self::map_error(resp));
        }
        Ok(resp.json()?)
    }

    /// Apps owned by the signed-in developer (`GET /games`).
    pub fn list_owned_games(&self) -> Result<Vec<OwnedGame>, ApiError> {
        let resp = self
            .http
            .get(self.url("/games"))
            .header(AUTHORIZATION, self.bearer()?)
            .send()?;
        if !resp.status().is_success() {
            return Err(Self::map_error(resp));
        }
        Ok(resp.json()?)
    }

    /// Fail unless the session owns `app_id` (checked via `GET /games`).
    pub fn require_app_ownership(&self, app_id: i64) -> Result<(), ApiError> {
        let owned = self.list_owned_games()?;
        if owns_app(&owned, app_id) {
            return Ok(());
        }
        Err(ApiError::Message(format!(
            "app {app_id} is not owned by this developer account: \
uploads and depot listing are limited to apps you own"
        )))
    }

    pub fn list_depots(&self, app_id: i64) -> Result<Vec<DepotRow>, ApiError> {
        self.require_app_ownership(app_id)?;
        let resp = self
            .http
            .get(self.url(&format!("/games/app/{app_id}/depots")))
            .header(AUTHORIZATION, self.bearer()?)
            .send()?;
        if !resp.status().is_success() {
            return Err(Self::map_error(resp));
        }
        Ok(resp.json()?)
    }

    pub fn init_content_upload(
        &self,
        app_id: i64,
        version: &str,
        total_bytes: i64,
        artifact_sha256: &str,
        depot_id: Option<&str>,
        filename: Option<&str>,
        chunk_size: Option<i64>,
    ) -> Result<InitUploadResponse, ApiError> {
        let resp = self
            .http
            .post(self.url(&format!(
                "/games/app/{app_id}/builds/content-upload/init"
            )))
            .header(AUTHORIZATION, self.bearer()?)
            .json(&InitBody {
                version,
                total_bytes,
                artifact_sha256,
                depot_id,
                filename,
                chunk_size,
            })
            .send()?;
        if !resp.status().is_success() {
            return Err(Self::map_error(resp));
        }
        Ok(resp.json()?)
    }

    pub fn put_chunk(&self, url: &str, bytes: &[u8]) -> Result<(), ApiError> {
        let resp = self
            .http
            .put(url)
            .header(CONTENT_TYPE, "application/octet-stream")
            .body(bytes.to_vec())
            .send()?;
        if resp.status() != StatusCode::NO_CONTENT && !resp.status().is_success() {
            return Err(Self::map_error(resp));
        }
        Ok(())
    }

    pub fn complete_content_upload(
        &self,
        app_id: i64,
        upload_id: Uuid,
    ) -> Result<CompleteUploadResponse, ApiError> {
        let resp = self
            .http
            .post(self.url(&format!(
                "/games/app/{app_id}/builds/content-upload/{upload_id}/complete"
            )))
            .header(AUTHORIZATION, self.bearer()?)
            .send()?;
        if !resp.status().is_success() {
            return Err(Self::map_error(resp));
        }
        Ok(resp.json()?)
    }

    pub fn register_build(
        &self,
        app_id: i64,
        version: &str,
        artifact_uri: &str,
        artifact_sha256: &str,
        depot_id: &str,
        changelog: Option<&str>,
        files: &[(String, String, u64)],
    ) -> Result<RegisteredBuild, ApiError> {
        let body = CreateBuildBody {
            version,
            artifact_uri,
            artifact_sha256,
            changelog,
            depot_id,
            files: files
                .iter()
                .map(|(path, sha, size)| ManifestFileBody {
                    path,
                    sha256: sha,
                    size_bytes: *size,
                })
                .collect(),
        };
        let resp = self
            .http
            .post(self.url(&format!("/games/app/{app_id}/builds")))
            .header(AUTHORIZATION, self.bearer()?)
            .json(&body)
            .send()?;
        if !resp.status().is_success() {
            return Err(Self::map_error(resp));
        }
        Ok(resp.json()?)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn url_joins_without_double_slash() {
        let c = ApiClient::new("http://localhost:8080/", None).unwrap();
        assert_eq!(c.url("/auth/login"), "http://localhost:8080/auth/login");
    }

    #[test]
    fn owns_app_matches_app_id() {
        let owned = vec![
            OwnedGame {
                app_id: 1000,
                title: Some("A".into()),
                id: None,
            },
            OwnedGame {
                app_id: 2000,
                title: None,
                id: None,
            },
        ];
        assert!(owns_app(&owned, 1000));
        assert!(owns_app(&owned, 2000));
        assert!(!owns_app(&owned, 3000));
    }

    #[test]
    fn api_depot_rows_use_depot_ids() {
        let rows: Vec<DepotRow> = serde_json::from_str(
            r#"[{"depot_id":"1000_00","depot_number":0,"name":"Base content","os":"any","arch":"any","language":"","game_id":"x"}]"#,
        )
        .unwrap();
        assert_eq!(rows[0].depot_id, "1000_00");
        assert_eq!(rows[0].depot_number, Some(0));
        let body = serde_json::to_value(InitBody {
            version: "1.0.0",
            total_bytes: 1,
            artifact_sha256: "ab",
            depot_id: Some("1000_01"),
            filename: None,
            chunk_size: None,
        })
        .unwrap();
        assert_eq!(body["depot_id"], "1000_01");
        assert!(body.get("depot_key").is_none());
    }
}
