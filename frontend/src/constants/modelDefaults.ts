/**
 * Default model names for transcription engines.
 * IMPORTANT: Keep in sync with Rust constants in src-tauri/src/config.rs
 */

/**
 * Default Whisper model for transcription when no preference is configured.
 * Keep the first-run download small so onboarding can complete quickly.
 */
export const DEFAULT_WHISPER_MODEL = 'tiny-q5_1';

/** Default transcription provider — smallest local setup uses Whisper. */
export const DEFAULT_TRANSCRIPTION_PROVIDER = 'localWhisper' as const;

/**
 * Model defaults by provider type
 */
export const MODEL_DEFAULTS = {
  whisper: DEFAULT_WHISPER_MODEL,
  localWhisper: DEFAULT_WHISPER_MODEL,
} as const;
