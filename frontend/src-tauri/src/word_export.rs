use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WordCapability {
    pub is_available: bool,
    pub prog_id_registered: bool,
    pub executable_path: Option<String>,
    pub office_version: Option<String>,
    pub office_products: Option<String>,
    pub platform: Option<String>,
    pub message: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TranscriptSegment {
    pub timestamp: Option<String>,
    pub speaker: Option<String>,
    pub text: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WordExportRequest {
    pub output_path: String,
    pub meeting_title: String,
    pub meeting_date: Option<String>,
    pub summary_markdown: Option<String>,
    #[serde(default)]
    pub transcript: Vec<TranscriptSegment>,
    #[serde(default)]
    pub open_after_export: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WordExportResponse {
    pub success: bool,
    pub output_path: Option<String>,
    pub error: Option<String>,
}

#[cfg(target_os = "windows")]
fn find_word_exporter() -> Option<std::path::PathBuf> {
    use std::path::{Path, PathBuf};

    if let Ok(configured) = std::env::var("NABRH_WORD_EXPORTER") {
        let path = PathBuf::from(configured);
        if path.is_file() {
            return Some(path);
        }
    }

    if let Ok(current_exe) = std::env::current_exe() {
        if let Some(directory) = current_exe.parent() {
            let bundled = directory.join("Nabrh.WordExporter.exe");
            if bundled.is_file() {
                return Some(bundled);
            }
        }
    }

    let mut roots = Vec::new();
    if let Ok(cwd) = std::env::current_dir() {
        roots.push(cwd.clone());
        roots.extend(cwd.ancestors().skip(1).take(4).map(Path::to_path_buf));
    }

    const RELATIVE_CANDIDATES: &[&str] = &[
        "Nabrh.WordExporter/bin/publish-word/Nabrh.WordExporter.exe",
        "Nabrh.WordExporter/bin/Release/net10.0-windows/win-x64/publish/Nabrh.WordExporter.exe",
        "Nabrh.WordExporter/bin/Debug/net10.0-windows/win-x64/Nabrh.WordExporter.exe",
    ];

    for root in roots {
        for candidate in RELATIVE_CANDIDATES {
            let path = root.join(candidate);
            if path.is_file() {
                return Some(path);
            }
        }
    }

    None
}

#[cfg(target_os = "windows")]
fn parse_process_json<T: serde::de::DeserializeOwned>(output: std::process::Output) -> Result<T, String> {
    let stdout = String::from_utf8_lossy(&output.stdout);
    let payload = stdout
        .lines()
        .rev()
        .find(|line| !line.trim().is_empty())
        .ok_or_else(|| {
            let stderr = String::from_utf8_lossy(&output.stderr);
            format!("Word exporter returned no response. {}", stderr.trim())
        })?;

    serde_json::from_str(payload)
        .map_err(|error| format!("Invalid response from Word exporter: {error}. Response: {payload}"))
}

#[tauri::command]
pub async fn word_export_capability() -> Result<WordCapability, String> {
    #[cfg(not(target_os = "windows"))]
    {
        Ok(WordCapability {
            is_available: false,
            prog_id_registered: false,
            executable_path: None,
            office_version: None,
            office_products: None,
            platform: None,
            message: "Word COM export is available on Windows only.".to_string(),
        })
    }

    #[cfg(target_os = "windows")]
    {
        tokio::task::spawn_blocking(|| {
            let exporter = find_word_exporter().ok_or_else(|| {
                "Nabrh.WordExporter.exe is not installed. Repair Nabrh to restore Word export."
                    .to_string()
            })?;

            let output = std::process::Command::new(exporter)
                .arg("--capability")
                .output()
                .map_err(|error| format!("Failed to start Word capability check: {error}"))?;

            parse_process_json(output)
        })
        .await
        .map_err(|error| format!("Word capability task failed: {error}"))?
    }
}

#[tauri::command]
pub async fn export_meeting_to_word(
    app: tauri::AppHandle,
    request: WordExportRequest,
) -> Result<WordExportResponse, String> {
    #[cfg(not(target_os = "windows"))]
    {
        let _ = request;
        Err("Word COM export is available on Windows only.".to_string())
    }

    #[cfg(target_os = "windows")]
    {
        use tauri_plugin_dialog::DialogExt;

        tokio::task::spawn_blocking(move || {
            use std::io::Write;

            let mut request = request;
            if request.output_path.trim().is_empty() {
                let safe_title: String = request
                    .meeting_title
                    .chars()
                    .map(|character| {
                        if matches!(character, '<' | '>' | ':' | '"' | '/' | '\\' | '|' | '?' | '*') {
                            '-'
                        } else {
                            character
                        }
                    })
                    .collect();
                let suggested_name = format!("{}.docx", safe_title.trim().trim_end_matches('.'));
                let selected_path = app
                    .dialog()
                    .file()
                    .add_filter("Microsoft Word document", &["docx"])
                    .set_file_name(if suggested_name == ".docx" {
                        "Nabrh meeting.docx".to_string()
                    } else {
                        suggested_name
                    })
                    .blocking_save_file();

                let Some(selected_path) = selected_path else {
                    return Ok(WordExportResponse {
                        success: false,
                        output_path: None,
                        error: None,
                    });
                };
                request.output_path = selected_path.to_string();
            }

            let exporter = find_word_exporter().ok_or_else(|| {
                "Nabrh.WordExporter.exe is not installed. Repair Nabrh to restore Word export."
                    .to_string()
            })?;

            let mut request_file = tempfile::Builder::new()
                .prefix("nabrh-word-export-")
                .suffix(".json")
                .tempfile()
                .map_err(|error| format!("Failed to create Word export request: {error}"))?;

            serde_json::to_writer(&mut request_file, &request)
                .map_err(|error| format!("Failed to serialize Word export request: {error}"))?;
            request_file
                .flush()
                .map_err(|error| format!("Failed to write Word export request: {error}"))?;

            let output = std::process::Command::new(exporter)
                .arg("--request")
                .arg(request_file.path())
                .output()
                .map_err(|error| format!("Failed to start Word exporter: {error}"))?;

            let response: WordExportResponse = parse_process_json(output)?;
            if response.success {
                Ok(response)
            } else {
                Err(response
                    .error
                    .unwrap_or_else(|| "Microsoft Word could not export the meeting.".to_string()))
            }
        })
        .await
        .map_err(|error| format!("Word export task failed: {error}"))?
    }
}
