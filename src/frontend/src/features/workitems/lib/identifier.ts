/**
 * The project key an item identifier carries (`BET-US-3` → `BET`), or `null` for a text that is no
 * identifier. A project's key is 2–10 Latin capital letters and digits starting with a letter and is
 * unique in its organization (mod-002 BR-M1-1), so on a team's view — whose address names no project —
 * the key finds the item's own project among the organization's active ones (feat-040 BR-TS-4).
 */
export function projectKeyOf(identifier: string): string | null {
  return /^([A-Z][A-Z0-9]{1,9})-[^-\s]+-[1-9][0-9]*$/.exec(identifier)?.[1] ?? null;
}
