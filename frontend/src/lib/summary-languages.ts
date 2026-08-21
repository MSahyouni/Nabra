export interface LanguageOption {
  code: string;
  label: string;
}

/**
 * Language options offered in the summary language pickers.
 * Codes must stay in sync with `language_name_from_code` in
 * `frontend/src-tauri/src/summary/processor.rs`.
 */
export const LANGUAGE_OPTIONS: LanguageOption[] = [
  { code: 'en', label: 'الإنجليزية' },
  { code: 'zh', label: 'الصينية' },
  { code: 'zh-tw', label: 'الصينية التقليدية' },
  { code: 'de', label: 'الألمانية' },
  { code: 'es', label: 'الإسبانية' },
  { code: 'ru', label: 'الروسية' },
  { code: 'ko', label: 'الكورية' },
  { code: 'fr', label: 'الفرنسية' },
  { code: 'ja', label: 'اليابانية' },
  { code: 'pt', label: 'البرتغالية' },
  { code: 'it', label: 'الإيطالية' },
  { code: 'nl', label: 'الهولندية' },
  { code: 'pl', label: 'البولندية' },
  { code: 'ar', label: 'العربية' },
  { code: 'hi', label: 'الهندية' },
  { code: 'ta', label: 'التاميلية' },
  { code: 'tr', label: 'التركية' },
  { code: 'vi', label: 'الفيتنامية' },
  { code: 'th', label: 'التايلاندية' },
  { code: 'id', label: 'الإندونيسية' },
  { code: 'sv', label: 'السويدية' },
  { code: 'cs', label: 'التشيكية' },
  { code: 'da', label: 'الدنماركية' },
  { code: 'fi', label: 'الفنلندية' },
  { code: 'el', label: 'اليونانية' },
  { code: 'he', label: 'العبرية' },
  { code: 'hu', label: 'المجرية' },
  { code: 'no', label: 'النرويجية' },
  { code: 'ro', label: 'الرومانية' },
  { code: 'uk', label: 'الأوكرانية' },
];

export const AUTO_VALUE = '__auto__' as const;

const SUPPORTED_CODES: ReadonlySet<string> = new Set(LANGUAGE_OPTIONS.map((o) => o.code));

/**
 * Normalises a raw locale string (from transcription or storage) into a code we
 * can translate into. Handles BCP-47 regional tags: `pt-BR` -> `pt`, `en_GB` -> `en`.
 * Returns null for unsupported languages so callers can fall back to English
 * rather than sending a code Rust will silently drop.
 */
export function normaliseLanguageCode(raw: string | null | undefined): string | null {
  if (!raw) return null;
  const lower = raw.toLowerCase().replace(/_/g, '-');
  if (SUPPORTED_CODES.has(lower)) return lower;
  const base = lower.split('-')[0];
  if (SUPPORTED_CODES.has(base)) return base;
  return null;
}

export function labelForCode(code: string): string {
  return LANGUAGE_OPTIONS.find((l) => l.code === code)?.label ?? code;
}
