/** One picker entry: `[emoji, Russian label, Russian tags (space separated), group index]`. */
export type EmojiEntry = readonly [string, string, string, number];

/** Case- and `ё`/`е`-folded, so «елка» finds «ёлка». */
export function fold(text: string): string {
  return text.toLowerCase().replaceAll('ё', 'е');
}

/**
 * The entries whose label or tags contain the folded query (BR-RT-19: the
 * search is by Russian name); an empty query is every entry, in the data's order.
 */
export function searchEmoji(entries: readonly EmojiEntry[], query: string): EmojiEntry[] {
  const needle = fold(query.trim());
  if (needle.length === 0) return [...entries];
  return entries.filter(
    ([, label, tags]) => fold(label).includes(needle) || fold(tags).includes(needle),
  );
}
