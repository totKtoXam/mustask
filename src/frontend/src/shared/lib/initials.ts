/**
 * The initials of an avatar (mod-015 US-2, design § 3.3): the first letters of
 * the first two space-separated words of the display name, upper-cased; one
 * word gives its first letter. `null` for an empty name — the avatar then
 * shows a person icon instead. The user menu and mod-003's assignee avatars
 * share it, so it lives here rather than in `app/`.
 *
 * «Иван Петров» → «ИП», «Иван Петрович Сидоров» → «ИП», «ivan.petrov» → «I»,
 * «e2e-fixture» → «E».
 */
export function initials(displayName: string): string | null {
  const words = displayName.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) {
    return null;
  }
  return words
    .slice(0, 2)
    .map((word) => Array.from(word)[0] ?? '')
    .join('')
    .toLocaleUpperCase('ru');
}
