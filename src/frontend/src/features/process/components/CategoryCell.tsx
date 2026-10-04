import Box from '@mui/material/Box';
import TableCell from '@mui/material/TableCell';
import type { Theme } from '@mui/material/styles';
import type { CategoryCode, StateCategory } from '../api/processApi';

/** The removed category is drawn as a ring, every other code as a dot (mod-015 design § 2.3). */
const REMOVED: CategoryCode = 'Removed';

/** Class name the tests address the marker by. */
export const CATEGORY_MARKER_CLASS = 'ProcessCategoryMarker';

function markerSx(code: string) {
  return (theme: Theme) => {
    const colour = theme.workTracking.categoryColour(code);
    const size = theme.layout.categoryDot;
    const shape = { width: size, height: size, borderRadius: '50%', flexShrink: 0 };
    return code === REMOVED
      ? {
          ...shape,
          boxSizing: 'border-box' as const,
          border: `${theme.spacing(0.25)} solid ${colour}`,
        }
      : { ...shape, backgroundColor: colour };
  };
}

/**
 * A state's category (design point 6): the marker in the category's theme
 * colour, looked up by its code, then the label exactly as the server sent
 * it. The client holds no code-to-label map (design R-26) and never shows a
 * code. The marker is `aria-hidden`, so colour is never the only carrier; an
 * unknown code still gets a dot, in the theme's secondary text colour.
 */
export function CategoryCell({ category }: { category: StateCategory }) {
  return (
    <TableCell>
      <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 1 }}>
        <Box
          component="span"
          aria-hidden="true"
          className={CATEGORY_MARKER_CLASS}
          sx={markerSx(category.code)}
        />
        {category.label}
      </Box>
    </TableCell>
  );
}
