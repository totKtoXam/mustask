const DASH = '—';

/** A whole-number percent, «63 %»; `null` (held is 0) is «—». */
export function percentText(value: number | null): string {
  return value === null ? DASH : `${Math.round(value)} %`;
}

/** A count; an average of counts has one decimal with the Russian comma, «6,3» (design D-10 B). */
export function countText(value: number | null): string {
  if (value === null) return DASH;
  return Number.isInteger(value) ? String(value) : value.toFixed(1).replace('.', ',');
}

/** A story-point total; the same shape as a count. */
export const pointsText = countText;
