import type { Theme } from '@mui/material/styles';

/**
 * The stripes both boards draw (feat-037; feat-060): the team board's and the aggregate board's,
 * one definition so they cannot drift apart. The colours are the theme's — nothing is restyled here.
 */

/** A column's status category as a stripe on top, in the colour of the category dots (the code is never shown). */
export function columnStripeSx(theme: Theme, categoryCode: string) {
  return {
    borderTop: `${theme.spacing(0.5)} solid ${theme.workTracking.categoryColour(categoryCode)}`,
  };
}

/** A card's type as a stripe at its left edge, in the colour of the type's mark. */
export function cardStripeSx(theme: Theme, appearance: string) {
  return {
    borderLeft: `${theme.spacing(0.5)} solid ${theme.workTracking.typeAppearance(appearance).colour}`,
  };
}
