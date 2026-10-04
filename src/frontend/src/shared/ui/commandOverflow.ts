/**
 * How many secondary commands stay in the command bar (design § 6.1).
 *
 * `widths` are the secondary commands' widths in their original order, each
 * with the gap that follows it; `available` is the width they may take beside
 * the primary action; `reserved` is the width of the «Ещё действия» button
 * that appears once anything overflows. Commands leave from the end until the
 * rest, plus that button, fit. The primary action is not counted here: it
 * never moves into the menu.
 */
export function splitCommands(
  widths: readonly number[],
  available: number,
  reserved: number,
): number {
  const total = widths.reduce((sum, width) => sum + width, 0);
  if (total <= available) {
    return widths.length;
  }
  let inline = widths.length;
  let used = total;
  while (inline > 0 && used + reserved > available) {
    inline -= 1;
    used -= widths[inline] ?? 0;
  }
  return inline;
}
