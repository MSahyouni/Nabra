/** Converts untrusted native/event progress into a safe percentage for text and CSS. */
export function normalizeDownloadProgress(value: unknown): number | null {
  const numeric = typeof value === 'number' ? value : Number(value);
  if (!Number.isFinite(numeric)) return null;
  return Math.min(100, Math.max(0, numeric));
}

export function downloadProgressOrZero(value: unknown): number {
  return normalizeDownloadProgress(value) ?? 0;
}
