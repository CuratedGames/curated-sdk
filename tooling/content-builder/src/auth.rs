//! Developer session credentials for the content builder.

use crate::client::{ApiClient, ApiError};
use serde::{Deserialize, Serialize};
use std::fs;
use std::io;
use std::path::PathBuf;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct Credentials {
    pub api_base: String,
    pub token: String,
    pub user_id: String,
    /// Email or username used at login (for display / whoami).
    #[serde(default)]
    pub login: Option<String>,
    /// Legacy field from older credential files; prefer `login`.
    #[serde(default)]
    pub email: Option<String>,
}

impl Credentials {
    pub fn display_login(&self) -> Option<&str> {
        self.login
            .as_deref()
            .filter(|s| !s.is_empty())
            .or(self.email.as_deref().filter(|s| !s.is_empty()))
    }
}

#[derive(Debug)]
pub enum LoginError {
    Io(io::Error),
    Api(ApiError),
    Message(String),
}

impl std::fmt::Display for LoginError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            LoginError::Io(e) => write!(f, "{e}"),
            LoginError::Api(e) => write!(f, "{e}"),
            LoginError::Message(m) => write!(f, "{m}"),
        }
    }
}

impl std::error::Error for LoginError {}

impl From<io::Error> for LoginError {
    fn from(value: io::Error) -> Self {
        LoginError::Io(value)
    }
}

impl From<ApiError> for LoginError {
    fn from(value: ApiError) -> Self {
        LoginError::Api(value)
    }
}

/// `%USERPROFILE%\.curated\credentials.json` on Windows; `~/.curated/` elsewhere.
pub fn credentials_path() -> PathBuf {
    if let Ok(override_path) = std::env::var("CURATED_CREDENTIALS_PATH") {
        return PathBuf::from(override_path);
    }
    let home = std::env::var_os("USERPROFILE")
        .or_else(|| std::env::var_os("HOME"))
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from("."));
    home.join(".curated").join("credentials.json")
}

pub fn save_credentials(creds: &Credentials) -> Result<PathBuf, LoginError> {
    let path = credentials_path();
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)?;
    }
    let json = serde_json::to_string_pretty(creds)
        .map_err(|e| LoginError::Message(format!("could not serialize credentials: {e}")))?;
    fs::write(&path, json)?;
    #[cfg(unix)]
    {
        use std::os::unix::fs::PermissionsExt;
        let _ = fs::set_permissions(&path, fs::Permissions::from_mode(0o600));
    }
    Ok(path)
}

pub fn load_credentials() -> Result<Option<Credentials>, LoginError> {
    let path = credentials_path();
    if !path.exists() {
        return Ok(None);
    }
    let raw = fs::read_to_string(&path)?;
    let mut creds: Credentials = serde_json::from_str(&raw)
        .map_err(|e| LoginError::Message(format!("could not read credentials: {e}")))?;
    // Normalize legacy email-only files into login.
    if creds.login.is_none() {
        creds.login = creds.email.clone();
    }
    Ok(Some(creds))
}

pub fn clear_credentials() -> Result<(), LoginError> {
    let path = credentials_path();
    if path.exists() {
        fs::remove_file(path)?;
    }
    Ok(())
}

/// Sign in with email or username + password; cache the session token locally.
pub fn login_and_store(
    api_base: &str,
    login: &str,
    password: &str,
) -> Result<Credentials, LoginError> {
    let login = login.trim();
    if login.is_empty() {
        return Err(LoginError::Message(
            "login (email or username) is required".into(),
        ));
    }
    if password.is_empty() {
        return Err(LoginError::Message("password is required".into()));
    }
    let client = ApiClient::new(api_base, None)?;
    let auth = client.login(login, password)?;
    if auth.token.trim().is_empty() {
        return Err(LoginError::Message(
            "login succeeded but API returned an empty token".into(),
        ));
    }
    let looks_like_email = login.contains('@');
    let creds = Credentials {
        api_base: api_base.trim_end_matches('/').to_string(),
        token: auth.token,
        user_id: auth.user_id,
        login: Some(login.to_string()),
        email: if looks_like_email {
            Some(login.to_string())
        } else {
            None
        },
    };
    // Prove the token works before caching (also confirms developer session).
    let verify = ApiClient::new(&creds.api_base, Some(&creds.token))?;
    let _ = verify.list_owned_games().map_err(|e| {
        LoginError::Message(format!(
            "signed in but could not list owned apps (developer account required): {e}"
        ))
    })?;
    let path = save_credentials(&creds)?;
    let _ = path;
    Ok(creds)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::Mutex;
    use tempfile::tempdir;

    static CRED_ENV_LOCK: Mutex<()> = Mutex::new(());

    #[test]
    fn round_trip_credentials_file() {
        let _guard = CRED_ENV_LOCK.lock().unwrap();
        let dir = tempdir().unwrap();
        let path = dir.path().join("credentials.json");
        std::env::set_var("CURATED_CREDENTIALS_PATH", &path);
        let creds = Credentials {
            api_base: "http://127.0.0.1:8080".into(),
            token: "tok".into(),
            user_id: "uid".into(),
            login: Some("dev@example.com".into()),
            email: Some("dev@example.com".into()),
        };
        save_credentials(&creds).unwrap();
        let loaded = load_credentials().unwrap().unwrap();
        assert_eq!(loaded, creds);
        assert_eq!(loaded.display_login(), Some("dev@example.com"));
        clear_credentials().unwrap();
        assert!(load_credentials().unwrap().is_none());
        std::env::remove_var("CURATED_CREDENTIALS_PATH");
    }

    #[test]
    fn legacy_email_only_file_normalizes_login() {
        let _guard = CRED_ENV_LOCK.lock().unwrap();
        let dir = tempdir().unwrap();
        let path = dir.path().join("credentials.json");
        std::env::set_var("CURATED_CREDENTIALS_PATH", &path);
        fs::write(
            &path,
            r#"{"api_base":"http://x","token":"t","user_id":"u","email":"a@b.c"}"#,
        )
        .unwrap();
        let loaded = load_credentials().unwrap().unwrap();
        assert_eq!(loaded.display_login(), Some("a@b.c"));
        std::env::remove_var("CURATED_CREDENTIALS_PATH");
    }

    #[test]
    fn login_rejects_empty_identity() {
        let err = login_and_store("http://127.0.0.1:9", "  ", "pw").unwrap_err();
        assert!(err.to_string().contains("required"));
    }
}
