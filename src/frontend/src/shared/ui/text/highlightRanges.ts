/** A half-open range `[start, end)` of UTF-16 offsets into the text. */
export type HighlightRange = readonly [start: number, end: number];

/** One UTF-16 unit, lower-cased and with «ё» as «е»; a unit whose case change changes the length stays. */
function fold(unit: string): string {
  const lower = unit.toLowerCase();
  return (lower.length === 1 ? lower : unit).replace('ё', 'е');
}

function foldAll(value: string): string {
  let out = '';
  for (let index = 0; index < value.length; index += 1) out += fold(value.charAt(index));
  return out;
}

/** Where the query occurs in the text: plain substring search, so no character is special. */
export function rangesOfQuery(text: string, query: string): HighlightRange[] {
  const needle = foldAll(query.trim());
  if (needle === '') return [];
  const haystack = foldAll(text);
  const found: HighlightRange[] = [];
  let from = 0;
  for (;;) {
    const at = haystack.indexOf(needle, from);
    if (at < 0) break;
    found.push([at, at + needle.length]);
    from = at + needle.length;
  }
  return found;
}

/** Valid, sorted, merged ranges inside the text; an invalid or empty one is dropped. */
export function normaliseRanges(
  ranges: readonly HighlightRange[],
  length: number,
): HighlightRange[] {
  const clean = ranges
    .map(([start, end]) => [Math.max(0, start), Math.min(length, end)] as const)
    .filter(([start, end]) => Number.isFinite(start) && Number.isFinite(end) && start < end)
    .sort((a, b) => a[0] - b[0]);
  const merged: [number, number][] = [];
  for (const [start, end] of clean) {
    const last = merged[merged.length - 1];
    if (last && start <= last[1]) last[1] = Math.max(last[1], end);
    else merged.push([start, end]);
  }
  return merged;
}
