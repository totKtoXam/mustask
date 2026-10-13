import type { NormalisedRichText } from './normalise';

/** BR-RT-7 (1): visible text of an html description, in Unicode code points. */
export const DESCRIPTION_MAX_VISIBLE = 100_000;

/** BR-RT-7 (2): the reduced markup, in Unicode code points. */
export const DESCRIPTION_MAX_MARKUP = 1_000_000;

/**
 * A request value above this many UTF-16 units is refused by the server before
 * it is parsed. The panel never sends one: its own guard (below) keeps under it.
 */
export const DESCRIPTION_RAW_CEILING = 2_000_000;

/** From this many visible characters the counter appears (design § States). */
export const DESCRIPTION_COUNTER_FROM = 90_000;

export type DescriptionVerdict = 'ok' | 'markup-too-large' | 'too-long';

/**
 * The client's answer to "may this be saved" (BR-RT-7, US-4): the markup
 * ceiling first, then the visible limit, so a value over both answers markup.
 * Both are checked before blank (BR-RT-6): a blank value over a limit is
 * refused, not stored as `''`. 100 000 exactly is within.
 */
export function checkDescription(value: NormalisedRichText): DescriptionVerdict {
  if (value.markupLength > DESCRIPTION_MAX_MARKUP) return 'markup-too-large';
  if (value.visibleLength > DESCRIPTION_MAX_VISIBLE) return 'too-long';
  return 'ok';
}
