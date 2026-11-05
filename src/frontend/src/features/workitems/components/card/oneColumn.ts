import type { Theme } from '@mui/material/styles';

/**
 * The container the kit's `ColumnLayout` declares and queries (tech-015 design § 5.8,
 * `@container kit-columns (…)`). A card's body reads its own width, never the window's.
 * The kit does not export the name, so `oneColumn.test.tsx` pins this copy to the one
 * `ColumnLayout` declares: a rename there fails that test, not a browser on a stand.
 */
export const COLUMNS_CONTAINER = 'kit-columns';

/**
 * In one column (the body below `layout.columns.twoMin`) the fields of a section take at
 * most `layout.formColumnWidth`, as feat-022 has it, and the texts take the column
 * (feat-029 US-3, «One column keeps the controls from stretching»). `sx` for a wrapper
 * round a section's fields; in two or three columns it does nothing.
 */
export function oneColumnCap(theme: Theme) {
  return {
    [`@container ${COLUMNS_CONTAINER} (max-width: ${theme.layout.columns.twoMin - 1}px)`]: {
      maxWidth: theme.layout.formColumnWidth,
    },
  };
}
