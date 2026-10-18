//! Build script (TOML) describing app + depot content mapping.

use serde::Deserialize;
use std::fs;
use std::path::{Path, PathBuf};

#[derive(Debug, Clone, Deserialize, PartialEq)]
pub struct BuildScript {
    pub app_id: i64,
    #[serde(default)]
    pub desc: Option<String>,
    pub content_root: PathBuf,
    #[serde(default)]
    pub build_output: Option<PathBuf>,
    pub version: String,
    #[serde(default)]
    pub depot_id: Option<String>,
    #[serde(default)]
    pub depot_key: Option<String>,
    #[serde(default)]
    pub filename: Option<String>,
    #[serde(default)]
    pub depots: Vec<DepotMapping>,
}

#[derive(Debug, Clone, Deserialize, PartialEq)]
pub struct DepotMapping {
    #[serde(default)]
    pub depot_id: Option<String>,
    #[serde(default)]
    pub depot_key: Option<String>,
    #[serde(default = "default_local_path")]
    pub local_path: String,
    #[serde(default = "default_depot_path")]
    pub depot_path: String,
    #[serde(default = "default_true")]
    pub recursive: bool,
}

fn default_local_path() -> String {
    "*".into()
}

fn default_depot_path() -> String {
    ".".into()
}

fn default_true() -> bool {
    true
}

#[derive(Debug)]
pub enum ScriptError {
    Io(std::io::Error),
    Parse(String),
    Message(String),
}

impl std::fmt::Display for ScriptError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            ScriptError::Io(e) => write!(f, "{e}"),
            ScriptError::Parse(e) => write!(f, "invalid build script: {e}"),
            ScriptError::Message(m) => write!(f, "{m}"),
        }
    }
}

impl std::error::Error for ScriptError {}

impl From<std::io::Error> for ScriptError {
    fn from(value: std::io::Error) -> Self {
        ScriptError::Io(value)
    }
}

/// Resolve content_root / build_output relative to the script file directory.
pub fn load_build_script(path: &Path) -> Result<BuildScript, ScriptError> {
    let raw = fs::read_to_string(path)?;
    let mut script: BuildScript = toml::from_str(&raw).map_err(|e| ScriptError::Parse(e.to_string()))?;
    if script.version.trim().is_empty() {
        return Err(ScriptError::Message("version is required".into()));
    }
    let base = path
        .parent()
        .map(Path::to_path_buf)
        .unwrap_or_else(|| PathBuf::from("."));
    if script.content_root.is_relative() {
        script.content_root = base.join(&script.content_root);
    }
    if let Some(out) = script.build_output.take() {
        script.build_output = Some(if out.is_relative() {
            base.join(out)
        } else {
            out
        });
    }
    // Promote first [[depots]] entry when top-level depot fields are empty.
    if script.depot_id.is_none() && script.depot_key.is_none() {
        if let Some(first) = script.depots.first() {
            script.depot_id = first.depot_id.clone();
            script.depot_key = first.depot_key.clone();
        }
    }
    Ok(script)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use tempfile::tempdir;

    #[test]
    fn loads_relative_content_root() {
        let dir = tempdir().unwrap();
        let content = dir.path().join("content");
        fs::create_dir_all(&content).unwrap();
        let script_path = dir.path().join("app_build.toml");
        let mut f = fs::File::create(&script_path).unwrap();
        writeln!(
            f,
            r#"
app_id = 1000
desc = "Simple build"
content_root = "content"
build_output = "output"
version = "1.0.0"
depot_key = "content"

[[depots]]
depot_key = "content"
local_path = "*"
depot_path = "."
recursive = true
"#
        )
        .unwrap();
        let script = load_build_script(&script_path).unwrap();
        assert_eq!(script.app_id, 1000);
        assert_eq!(script.version, "1.0.0");
        assert_eq!(script.content_root, content);
        assert_eq!(script.depot_key.as_deref(), Some("content"));
        assert_eq!(script.build_output.unwrap(), dir.path().join("output"));
    }
}
