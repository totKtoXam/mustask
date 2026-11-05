import Box from '@mui/material/Box';

export interface TypeStripeProps {
  /** The type's stored appearance token — its colour is the bar's. */
  appearance: string;
  /** `card` is the header's 10 px bar, `compact` the board card's 4 px one. Default `card`. */
  size?: 'card' | 'compact';
}

/**
 * The coloured bar at the left edge of a work item surface (tech-015 design § 5.9): the
 * card's header (`layout.card.typeBar`) and the board's card (`typeBarCompact`). A flex
 * item that stretches to its row's height, so a host puts it first in a flex row.
 *
 * `aria-hidden`: colour is never the only carrier — the type's name stands in the type
 * line or in the card's text.
 */
export function TypeStripe({ appearance, size = 'card' }: TypeStripeProps) {
  return (
    <Box
      component="span"
      aria-hidden="true"
      data-type-stripe={size}
      sx={(theme) => ({
        display: 'block',
        flexShrink: 0,
        alignSelf: 'stretch',
        width: size === 'card' ? theme.layout.card.typeBar : theme.layout.card.typeBarCompact,
        backgroundColor: theme.workTracking.typeAppearance(appearance).colour,
      })}
    />
  );
}
