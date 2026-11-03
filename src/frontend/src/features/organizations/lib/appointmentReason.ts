/** BR-RS-5: the reason is 1–500 Unicode code points once trimmed. */
export const REASON_MAX_CODE_POINTS = 500;

/**
 * The reason as it is checked and sent: `String.prototype.trim()`, as the server trims
 * (feat-033 api-contract § 1.3, QA D-1) — so U+FEFF goes, U+0085 stays.
 */
export function trimmedReason(typed: string): string {
  return typed.trim();
}

/** Code points, not UTF-16 units: an emoji is one (DC-6). */
export function reasonLength(typed: string): number {
  return Array.from(trimmedReason(typed)).length;
}

export type ReasonProblem = 'required' | 'tooLong';

/** What is wrong with the reason, or `null` when it may be sent. */
export function reasonProblem(typed: string): ReasonProblem | null {
  const length = reasonLength(typed);
  if (length === 0) {
    return 'required';
  }
  return length > REASON_MAX_CODE_POINTS ? 'tooLong' : null;
}
