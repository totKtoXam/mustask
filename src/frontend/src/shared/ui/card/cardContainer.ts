import type { Theme } from '@mui/material/styles';

/**
 * The container the card's parts query (tech-015 design § 5.9): `@container kit-card (…)`.
 * An element cannot query itself, so the shell's outer surface — `DialogShell`'s card
 * Paper or `PanelShell`'s `Surface`, and nothing else — declares it, and the header and
 * the band, which are its descendants, read it.
 */
export const CARD_CONTAINER = 'kit-card';

/**
 * The custom property a `DialogShell` sets on the header's slot: how far from the card's
 * right edge the dialog's own icons (⤢ and ✕) reach, so the type line stops short of
 * them. Outside a dialog it is unset, and the line runs to the header's edge.
 */
export const CARD_ICONS_INSET = '--kit-card-icons';

/** The declaration; `inline-size` containment, so only the width is queried. */
export const cardContainerSx = {
  containerType: 'inline-size',
  containerName: CARD_CONTAINER,
} as const;

/**
 * The query under which the card is narrow: the header's third row wraps and the band's
 * blocks stack. `layout.card.stackBelow` is the least width that is still wide, so the
 * query stops a hair under it.
 */
export function narrowCardQuery(theme: Theme): string {
  return `@container ${CARD_CONTAINER} (max-width: ${theme.layout.card.stackBelow - 0.02}px)`;
}
