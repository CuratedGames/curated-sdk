//! Curated Content Builder: Windows-first CLI to upload depot builds.

use clap::{Parser, Subcommand};
use curated_content_builder::{
    clear_credentials, credentials_path, load_build_script, load_credentials, login_and_store,
    run_upload, validate_depot_id, ApiClient, UploadOptions, DEFAULT_API_BASE,
};
use std::io::IsTerminal;
use std::path::PathBuf;
use std::process::ExitCode;

#[derive(Parser, Debug)]
#[command(
    name = "curated-content-builder",
    about = "Upload game content to a Curated depot (draft and unreleased apps included).",
    long_about = "Sign in as a developer, target an app and depot you own, point at a content \
folder (or a build script), upload the payload, and register the build. Codesign is not required \
for this tool."
)]
struct Cli {
    #[command(subcommand)]
    command: Commands,
}

#[derive(Subcommand, Debug)]
enum Commands {
    /// Sign in with email or username + password; cache the session for later commands.
    Login {
        /// Email or username (alias of --email / --username).
        #[arg(long, env = "CURATED_LOGIN")]
        login: Option<String>,
        #[arg(long, env = "CURATED_EMAIL")]
        email: Option<String>,
        #[arg(long, env = "CURATED_USERNAME")]
        username: Option<String>,
        /// Account password. Omit this flag to type it at a prompt.
        /// Bash history-expands ! inside double quotes before this program starts; single quotes pass ! through.
        #[arg(long, env = "CURATED_PASSWORD")]
        password: Option<String>,
        #[arg(long, env = "CURATED_API_BASE", default_value = DEFAULT_API_BASE)]
        api_base: String,
    },
    /// Remove the stored session.
    Logout,
    /// Show the cached session (if any).
    Whoami,
    /// List depots for an app you own.
    ListDepots {
        #[arg(long)]
        app_id: i64,
        #[arg(long, env = "CURATED_API_BASE")]
        api_base: Option<String>,
        #[arg(long, env = "CURATED_TOKEN")]
        token: Option<String>,
    },
    /// Pack a content directory, upload it, and register a build on a depot you own.
    Upload {
        /// Path to a TOML build script (see examples/simple_app_build.toml).
        #[arg(long)]
        script: Option<PathBuf>,
        #[arg(long)]
        app_id: Option<i64>,
        #[arg(long)]
        content: Option<PathBuf>,
        #[arg(long)]
        version: Option<String>,
        /// Depot ID `<app_id>_<NN>` (see list-depots). Default: the base depot `<app_id>_00`.
        #[arg(long, env = "CURATED_DEPOT_ID")]
        depot_id: Option<String>,
        #[arg(long)]
        desc: Option<String>,
        #[arg(long)]
        filename: Option<String>,
        #[arg(long)]
        build_output: Option<PathBuf>,
        /// Chunk size in bytes (64 KiB..32 MiB). Default: adaptive from a short bandwidth probe.
        #[arg(long)]
        chunk_size: Option<i64>,
        /// Parallel chunk PUTs (1..8). Default: adaptive from measured bandwidth.
        #[arg(long)]
        concurrency: Option<usize>,
        #[arg(long, env = "CURATED_API_BASE")]
        api_base: Option<String>,
        #[arg(long, env = "CURATED_TOKEN")]
        token: Option<String>,
    },
}

fn resolve_login_identity(
    login: Option<String>,
    email: Option<String>,
    username: Option<String>,
) -> Result<String, String> {
    login
        .or(email)
        .or(username)
        .map(|s| s.trim().to_string())
        .filter(|s| !s.is_empty())
        .ok_or_else(|| {
            "provide --login, --email, or --username (email or username + password)".to_string()
        })
}

fn resolve_password(password: Option<String>) -> Result<String, String> {
    if let Some(password) = password {
        if password.is_empty() {
            return Err("password is required".into());
        }
        return Ok(password);
    }
    if !std::io::stdin().is_terminal() {
        return Err("password is required: pass --password or set CURATED_PASSWORD".into());
    }
    let password = rpassword::prompt_password("Password: ").map_err(|e| e.to_string())?;
    if password.is_empty() {
        return Err("password is required".into());
    }
    Ok(password)
}

fn resolve_client(
    api_base_flag: Option<String>,
    token_flag: Option<String>,
) -> Result<ApiClient, String> {
    let creds = load_credentials().map_err(|e| e.to_string())?;
    let api_base = api_base_flag
        .or_else(|| creds.as_ref().map(|c| c.api_base.clone()))
        .unwrap_or_else(|| DEFAULT_API_BASE.to_string());
    let token = token_flag
        .or_else(|| creds.as_ref().map(|c| c.token.clone()))
        .ok_or_else(|| {
            "not signed in: run `curated-content-builder login` or pass --token".to_string()
        })?;
    ApiClient::new(&api_base, Some(&token)).map_err(|e| e.to_string())
}

/// Validate `--depot-id` / script `depot_id` locally (`<app_id>_<NN>`) before uploading.
fn parse_depot_id(raw: Option<&str>, app_id: i64) -> Result<Option<String>, String> {
    raw.map(|s| validate_depot_id(s, app_id)).transpose()
}

fn main() -> ExitCode {
    let cli = Cli::parse();
    match run(cli) {
        Ok(()) => ExitCode::SUCCESS,
        Err(e) => {
            eprintln!("error: {e}");
            ExitCode::FAILURE
        }
    }
}

fn run(cli: Cli) -> Result<(), String> {
    match cli.command {
        Commands::Login {
            login,
            email,
            username,
            password,
            api_base,
        } => {
            let identity = resolve_login_identity(login, email, username)?;
            let password = resolve_password(password)?;
            let creds =
                login_and_store(&api_base, &identity, &password).map_err(|e| e.to_string())?;
            println!(
                "Signed in as {} (user {}). Session saved to {}.",
                creds.display_login().unwrap_or(&identity),
                creds.user_id,
                credentials_path().display()
            );
            Ok(())
        }
        Commands::Logout => {
            clear_credentials().map_err(|e| e.to_string())?;
            println!("Signed out.");
            Ok(())
        }
        Commands::Whoami => {
            match load_credentials().map_err(|e| e.to_string())? {
                None => {
                    println!("Not signed in.");
                    println!("Run: curated-content-builder login --login you@studio.com --password \"…\"");
                }
                Some(creds) => {
                    println!(
                        "login: {}",
                        creds.display_login().unwrap_or("(unknown)")
                    );
                    println!("user_id: {}", creds.user_id);
                    println!("api_base: {}", creds.api_base);
                    println!("credentials: {}", credentials_path().display());
                }
            }
            Ok(())
        }
        Commands::ListDepots {
            app_id,
            api_base,
            token,
        } => {
            let client = resolve_client(api_base, token)?;
            let depots = client.list_depots(app_id).map_err(|e| e.to_string())?;
            if depots.is_empty() {
                println!("No depots for app {app_id}.");
                return Ok(());
            }
            println!("{:<12} {:<32} {}", "DEPOT ID", "NAME", "OS/ARCH/LANG");
            for d in depots {
                let language = d.language.as_deref().unwrap_or("");
                println!(
                    "{:<12} {:<32} {}/{}/{}",
                    d.depot_id,
                    d.name,
                    d.os.as_deref().unwrap_or("-"),
                    d.arch.as_deref().unwrap_or("-"),
                    if language.is_empty() { "all" } else { language }
                );
            }
            println!();
            println!("Upload with: curated-content-builder upload --app-id {app_id} --depot-id {app_id}_00 ...");
            Ok(())
        }
        Commands::Upload {
            script,
            app_id,
            content,
            version,
            depot_id,
            desc,
            filename,
            build_output,
            chunk_size,
            concurrency,
            api_base,
            token,
        } => {
            let opts = if let Some(script_path) = script.as_ref() {
                let s = load_build_script(script_path).map_err(|e| e.to_string())?;
                let app_id = app_id.unwrap_or(s.app_id);
                UploadOptions {
                    app_id,
                    content_root: content.unwrap_or(s.content_root),
                    version: version.unwrap_or(s.version),
                    depot_id: parse_depot_id(
                        depot_id.as_deref().or(s.depot_id.as_deref()),
                        app_id,
                    )?,
                    desc: desc.or(s.desc),
                    filename: filename.or(s.filename),
                    build_output: build_output.or(s.build_output),
                    chunk_size,
                    concurrency,
                }
            } else {
                let app_id = app_id.ok_or("--app-id is required without --script")?;
                UploadOptions {
                    app_id,
                    content_root: content.ok_or("--content is required without --script")?,
                    version: version.ok_or("--version is required without --script")?,
                    depot_id: parse_depot_id(depot_id.as_deref(), app_id)?,
                    desc,
                    filename,
                    build_output,
                    chunk_size,
                    concurrency,
                }
            };

            let client = resolve_client(api_base, token)?;
            let result = run_upload(&client, opts).map_err(|e| e.to_string())?;
            println!(
                "Build {} registered on depot {} ({} files → {}; {} byte chunks × {}).",
                result.build.id,
                result.depot_id,
                result.file_count,
                result.artifact_uri,
                result.chunk_size,
                result.concurrency
            );
            Ok(())
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn resolve_login_prefers_login_then_email_then_username() {
        assert_eq!(
            resolve_login_identity(Some("a@b.c".into()), Some("x".into()), None).unwrap(),
            "a@b.c"
        );
        assert_eq!(
            resolve_login_identity(None, Some("a@b.c".into()), Some("u".into())).unwrap(),
            "a@b.c"
        );
        assert_eq!(
            resolve_login_identity(None, None, Some("studio".into())).unwrap(),
            "studio"
        );
        assert!(resolve_login_identity(None, None, None).is_err());
    }

    #[test]
    fn password_with_exclamation_mark_is_kept() {
        let cli = Cli::try_parse_from([
            "curated-content-builder",
            "login",
            "--username",
            "Someone",
            "--password",
            "BLA123BLA!ASADSFDSFS",
        ])
        .unwrap();
        match cli.command {
            Commands::Login { password, .. } => {
                assert_eq!(password.as_deref(), Some("BLA123BLA!ASADSFDSFS"));
            }
            _ => panic!("expected login"),
        }
    }

    #[test]
    fn depot_id_flag_is_validated_and_depot_key_is_gone() {
        let cli = Cli::try_parse_from([
            "curated-content-builder",
            "upload",
            "--app-id",
            "1000",
            "--depot-id",
            "1000_01",
            "--content",
            "c",
            "--version",
            "1",
        ])
        .unwrap();
        match cli.command {
            Commands::Upload { depot_id, .. } => assert_eq!(depot_id.as_deref(), Some("1000_01")),
            _ => panic!("expected upload"),
        }
        assert!(Cli::try_parse_from([
            "curated-content-builder",
            "upload",
            "--app-id",
            "1000",
            "--depot-key",
            "content",
        ])
        .is_err());
        assert_eq!(
            parse_depot_id(Some("1000_00"), 1000).unwrap().as_deref(),
            Some("1000_00")
        );
        assert_eq!(parse_depot_id(None, 1000).unwrap(), None);
        let e = parse_depot_id(Some("content"), 1000).unwrap_err();
        assert!(e.contains("<app_id>_<NN>"), "{e}");
    }
}
