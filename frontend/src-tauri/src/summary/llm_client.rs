use reqwest::{header, Client};
use serde::{Deserialize, Serialize};
use std::path::PathBuf;
use std::time::Duration;
use tokio_util::sync::CancellationToken;
use tracing::info;

const REQUEST_TIMEOUT_DURATION: Duration = Duration::from_secs(300);

// Generic structure for OpenAI-compatible API chat messages
#[derive(Debug, Serialize)]
pub struct ChatMessage {
    pub role: String,
    pub content: String,
}

// Generic structure for OpenAI-compatible API chat requests
#[derive(Debug, Serialize)]
pub struct ChatRequest {
    pub model: String,
    pub messages: Vec<ChatMessage>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub max_tokens: Option<u32>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub temperature: Option<f32>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub top_p: Option<f32>,
}

// Generic structure for OpenAI-compatible API chat responses
#[derive(Deserialize, Debug)]
pub struct ChatResponse {
    pub choices: Vec<Choice>,
}

#[derive(Deserialize, Debug)]
pub struct Choice {
    pub message: MessageContent,
}

#[derive(Deserialize, Debug)]
pub struct MessageContent {
    pub content: String,
}

// ---------------------------------------------------------------------------
// Ollama native /api/chat
//
// Ollama's OpenAI-compatible /v1/chat/completions endpoint has no way to set the
// runtime context window: it is documented as unsupported, and Ollama silently
// falls back to its default num_ctx (4096 in current builds).
//
// That was actively harmful here. SummaryService derives token_threshold from the
// model's *architectural* context reported by /api/show — 131072 for Llama 3.1 —
// and processor::generate_meeting_summary then sends a chunk of that size as a
// single prompt. Ollama truncated it to 4096 tokens with no error, so a two-hour
// meeting was summarised from only its last few minutes and the result looked
// perfectly plausible.
//
// The native endpoint accepts an options object, so Ollama now goes through it.
// ---------------------------------------------------------------------------

#[derive(Debug, Serialize)]
pub struct OllamaOptions {
    pub num_ctx: u32,
    pub temperature: f32,
}

#[derive(Debug, Serialize)]
pub struct OllamaChatRequest {
    pub model: String,
    pub messages: Vec<ChatMessage>,
    pub stream: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub format: Option<serde_json::Value>,
    pub think: bool,
    pub keep_alive: String,
    pub options: OllamaOptions,
}

#[derive(Deserialize, Debug)]
pub struct OllamaChatResponse {
    pub message: OllamaResponseMessage,
}

#[derive(Deserialize, Debug)]
pub struct OllamaResponseMessage {
    pub content: String,
}

/// Summarisation is an extractive task: the output must follow the transcript, not
/// improvise. Ollama's own default is 0.8, which produced unstable summaries and
/// invented action items.
pub const OLLAMA_SUMMARY_TEMPERATURE: f32 = 0.2;

/// Tokens held back from the context window for the system prompt and the model's own
/// output when deriving a chunk size.
pub const OLLAMA_CONTEXT_RESERVE_TOKENS: usize = 300;

/// Upper bound on the num_ctx we will ask Ollama for, regardless of what the model
/// architecturally supports.
///
/// num_ctx sizes the KV cache and it is allocated up front. For an 8B GQA model
/// (32 layers, 8 KV heads, 128 head dim, fp16) the cache is about 2 GB at 16k tokens
/// and roughly 17 GB at the 131072 that Llama 3.1 reports — enough to fail or thrash
/// on any consumer GPU. Transcripts longer than this cap are handled by the existing
/// multi-level chunk-and-combine path, which is what it is there for.
///
/// Raise this only alongside a check of the user's available VRAM.
pub const MAX_OLLAMA_CONTEXT_TOKENS: usize = 8192;

// Claude-specific request structure
#[derive(Debug, Serialize)]
pub struct ClaudeRequest {
    pub model: String,
    pub max_tokens: u32,
    pub system: String,
    pub messages: Vec<ChatMessage>,
}

// Claude-specific response structure
#[derive(Deserialize, Debug)]
pub struct ClaudeChatResponse {
    pub content: Vec<ClaudeChatContent>,
}

#[derive(Deserialize, Debug)]
pub struct ClaudeChatContent {
    pub text: String,
}

/// LLM Provider enumeration for multi-provider support
#[derive(Debug, Clone, PartialEq)]
pub enum LLMProvider {
    OpenAI,
    Claude,
    Groq,
    Ollama,
    OpenRouter,
    BuiltInAI,
    CustomOpenAI,
}

impl LLMProvider {
    /// Parse provider from string (case-insensitive)
    pub fn from_str(s: &str) -> Result<Self, String> {
        match s.to_lowercase().as_str() {
            "openai" => Ok(Self::OpenAI),
            "claude" => Ok(Self::Claude),
            "groq" => Ok(Self::Groq),
            "ollama" => Ok(Self::Ollama),
            "openrouter" => Ok(Self::OpenRouter),
            "builtin-ai" | "local-llama" | "localllama" => Ok(Self::BuiltInAI),
            "custom-openai" => Ok(Self::CustomOpenAI),
            _ => Err(format!("Unsupported LLM provider: {}", s)),
        }
    }
}

/// Generates a summary through Ollama's native `/api/chat` endpoint.
///
/// Unlike the OpenAI-compatible `/v1/chat/completions` shim this accepts an `options`
/// object, which is the only way to set `num_ctx`. Without it Ollama applies its own
/// default context (4096) and silently truncates anything longer — see the
/// `OllamaChatRequest` comment for the failure this caused.
async fn generate_summary_ollama(
    client: &Client,
    model_name: &str,
    system_prompt: &str,
    user_prompt: &str,
    ollama_endpoint: Option<&str>,
    num_ctx: Option<u32>,
    response_format: Option<serde_json::Value>,
    cancellation_token: Option<&CancellationToken>,
) -> Result<String, String> {
    let host = ollama_endpoint.unwrap_or("http://localhost:11434");
    let api_url = format!("{}/api/chat", host.trim_end_matches('/'));

    let effective_num_ctx = num_ctx.unwrap_or(MAX_OLLAMA_CONTEXT_TOKENS as u32);

    let request_body = OllamaChatRequest {
        model: model_name.to_string(),
        messages: vec![
            ChatMessage {
                role: "system".to_string(),
                content: system_prompt.to_string(),
            },
            ChatMessage {
                role: "user".to_string(),
                content: user_prompt.to_string(),
            },
        ],
        stream: false,
        format: response_format,
        think: false,
        keep_alive: "10m".to_string(),
        options: OllamaOptions {
            num_ctx: effective_num_ctx,
            temperature: OLLAMA_SUMMARY_TEMPERATURE,
        },
    };

    info!(
        "🐞 LLM Request to Ollama (/api/chat): model={}, num_ctx={}, temperature={}",
        model_name, effective_num_ctx, OLLAMA_SUMMARY_TEMPERATURE
    );

    let request_future = client
        .post(&api_url)
        .json(&request_body)
        .timeout(REQUEST_TIMEOUT_DURATION)
        .send();

    let response = match cancellation_token {
        Some(token) => tokio::select! {
            result = request_future => result.map_err(map_request_error)?,
            _ = token.cancelled() => return Err("Summary generation was cancelled".to_string()),
        },
        None => request_future.await.map_err(map_request_error)?,
    };

    if !response.status().is_success() {
        let status = response.status();
        let error_body = response
            .text()
            .await
            .unwrap_or_else(|_| "Unknown error".to_string());
        return Err(format!("Ollama request failed ({}): {}", status, error_body));
    }

    let parsed: OllamaChatResponse = response
        .json()
        .await
        .map_err(|e| format!("Failed to parse Ollama response: {}", e))?;

    Ok(parsed.message.content)
}

fn map_request_error(e: reqwest::Error) -> String {
    if e.is_timeout() {
        format!(
            "LLM request timed out after {} seconds",
            REQUEST_TIMEOUT_DURATION.as_secs()
        )
    } else {
        format!("Failed to send request to LLM: {}", e)
    }
}

/// Generates a summary using the specified LLM provider
///
/// # Arguments
/// * `client` - Reqwest HTTP client (reused for performance)
/// * `provider` - The LLM provider to use
/// * `model_name` - The specific model to use (e.g., "gpt-4", "claude-3-opus")
/// * `api_key` - API key for the provider (not needed for Ollama)
/// * `system_prompt` - System instructions for the LLM
/// * `user_prompt` - User query/content to process
/// * `ollama_endpoint` - Optional custom Ollama endpoint (defaults to localhost:11434)
/// * `custom_openai_endpoint` - Optional custom OpenAI-compatible endpoint
/// * `max_tokens` - Optional max tokens (for CustomOpenAI provider)
/// * `temperature` - Optional temperature (for CustomOpenAI provider)
/// * `top_p` - Optional top_p (for CustomOpenAI provider)
/// * `app_data_dir` - Optional app data directory (for BuiltInAI provider)
/// * `cancellation_token` - Optional token to cancel the request
///
/// # Returns
/// The generated summary text or an error message
pub async fn generate_summary(
    client: &Client,
    provider: &LLMProvider,
    model_name: &str,
    api_key: &str,
    system_prompt: &str,
    user_prompt: &str,
    ollama_endpoint: Option<&str>,
    custom_openai_endpoint: Option<&str>,
    max_tokens: Option<u32>,
    temperature: Option<f32>,
    top_p: Option<f32>,
    ollama_num_ctx: Option<u32>,
    response_format: Option<serde_json::Value>,
    app_data_dir: Option<&PathBuf>,
    cancellation_token: Option<&CancellationToken>,
) -> Result<String, String> {
    // Check if cancelled before starting
    if let Some(token) = cancellation_token {
        if token.is_cancelled() {
            return Err("Summary generation was cancelled".to_string());
        }
    }

    // Handle BuiltInAI provider separately (uses local sidecar, no HTTP API)
    if provider == &LLMProvider::BuiltInAI {
        let app_data_dir = app_data_dir
            .ok_or_else(|| "app_data_dir is required for BuiltInAI provider".to_string())?;

        return crate::summary::summary_engine::generate_with_builtin(
            app_data_dir,
            model_name,
            system_prompt,
            user_prompt,
            cancellation_token,
        )
        .await
        .map_err(|e| e.to_string());
    }

    // Ollama uses its native /api/chat rather than the OpenAI-compatible shim, because only
    // the native endpoint accepts an options object — and therefore num_ctx. See the
    // OllamaChatRequest definition above for why that matters.
    if provider == &LLMProvider::Ollama {
        return generate_summary_ollama(
            client,
            model_name,
            system_prompt,
            user_prompt,
            ollama_endpoint,
            ollama_num_ctx,
            response_format,
            cancellation_token,
        )
        .await;
    }

    let (api_url, mut headers) = match provider {
        LLMProvider::OpenAI => (
            "https://api.openai.com/v1/chat/completions".to_string(),
            header::HeaderMap::new(),
        ),
        LLMProvider::Groq => (
            "https://api.groq.com/openai/v1/chat/completions".to_string(),
            header::HeaderMap::new(),
        ),
        LLMProvider::OpenRouter => (
            "https://openrouter.ai/api/v1/chat/completions".to_string(),
            header::HeaderMap::new(),
        ),
        LLMProvider::Ollama => {
            // Handled above via the native /api/chat endpoint.
            unreachable!("Ollama is handled before this match statement")
        }
        LLMProvider::CustomOpenAI => {
            let endpoint = custom_openai_endpoint
                .ok_or_else(|| "Custom OpenAI endpoint not configured".to_string())?;
            (
                format!("{}/chat/completions", endpoint.trim_end_matches('/')),
                header::HeaderMap::new(),
            )
        }
        LLMProvider::Claude => {
            let mut header_map = header::HeaderMap::new();
            header_map.insert(
                "x-api-key",
                api_key
                    .parse()
                    .map_err(|_| "Invalid API key format".to_string())?,
            );
            header_map.insert(
                "anthropic-version",
                "2023-06-01"
                    .parse()
                    .map_err(|_| "Invalid anthropic version".to_string())?,
            );
            ("https://api.anthropic.com/v1/messages".to_string(), header_map)
        }
        LLMProvider::BuiltInAI => {
            // This case is handled earlier with early returns
            unreachable!("BuiltInAI is handled before this match statement")
        }
    };

    // Add authorization header for non-Claude providers
    if provider != &LLMProvider::Claude {
        headers.insert(
            header::AUTHORIZATION,
            format!("Bearer {}", api_key)
                .parse()
                .map_err(|_| "Invalid authorization header".to_string())?,
        );
    }
    headers.insert(
        header::CONTENT_TYPE,
        "application/json"
            .parse()
            .map_err(|_| "Invalid content type".to_string())?,
    );

    // Build request body based on provider
    let request_body = if provider != &LLMProvider::Claude {
        // For CustomOpenAI, apply optional parameters if provided
        let (max_tokens_val, temperature_val, top_p_val) = if provider == &LLMProvider::CustomOpenAI {
            (max_tokens, temperature, top_p)
        } else {
            (None, None, None)
        };

        serde_json::json!(ChatRequest {
            model: model_name.to_string(),
            messages: vec![
                ChatMessage {
                    role: "system".to_string(),
                    content: system_prompt.to_string(),
                },
                ChatMessage {
                    role: "user".to_string(),
                    content: user_prompt.to_string(),
                }
            ],
            max_tokens: max_tokens_val,
            temperature: temperature_val,
            top_p: top_p_val,
        })
    } else {
        serde_json::json!(ClaudeRequest {
            system: system_prompt.to_string(),
            model: model_name.to_string(),
            max_tokens: 2048,
            messages: vec![ChatMessage {
                role: "user".to_string(),
                content: user_prompt.to_string(),
            }]
        })
    };

    info!("🐞 LLM Request to {}: model={}", provider_name(provider), model_name);

    // Send request with timeout and cancellation support
    let request_future = client
        .post(api_url)
        .headers(headers)
        .json(&request_body)
        .timeout(REQUEST_TIMEOUT_DURATION)
        .send();

    // Use tokio::select to race between cancellation and request completion
    let response = if let Some(token) = cancellation_token {
        tokio::select! {
            result = request_future => {
                result.map_err(map_request_error)?
            }
            _ = token.cancelled() => {
                return Err("Summary generation was cancelled".to_string());
            }
        }
    } else {
        request_future.await.map_err(map_request_error)?
    };

    if !response.status().is_success() {
        let error_body = response
            .text()
            .await
            .unwrap_or_else(|_| "Unknown error".to_string());
        return Err(format!("LLM API request failed: {}", error_body));
    }

    // Parse response based on provider
    if provider == &LLMProvider::Claude {
        let chat_response = response
            .json::<ClaudeChatResponse>()
            .await
            .map_err(|e| format!("Failed to parse LLM response: {}", e))?;

        info!("🐞 LLM Response received from Claude");

        let content = chat_response
            .content
            .get(0)
            .ok_or("No content in LLM response")?
            .text
            .trim();
        Ok(content.to_string())
    } else {
        let chat_response = response
            .json::<ChatResponse>()
            .await
            .map_err(|e| format!("Failed to parse LLM response: {}", e))?;

        info!("🐞 LLM Response received from {}", provider_name(provider));

        let content = chat_response
            .choices
            .get(0)
            .ok_or("No content in LLM response")?
            .message
            .content
            .trim();
        Ok(content.to_string())
    }
}

/// Helper function to get provider name for logging
fn provider_name(provider: &LLMProvider) -> &str {
    match provider {
        LLMProvider::OpenAI => "OpenAI",
        LLMProvider::Claude => "Claude",
        LLMProvider::Groq => "Groq",
        LLMProvider::Ollama => "Ollama",
        LLMProvider::BuiltInAI => "Built-in AI",
        LLMProvider::OpenRouter => "OpenRouter",
        LLMProvider::CustomOpenAI => "Custom OpenAI",
    }
}
