import Box from '@mui/material/Box';
import TableCell from '@mui/material/TableCell';
import { CategoryMarker } from '../../../shared/ui/WorkTrackingMarks';
import type { StateCategory } from '../api/processApi';

/** Class name the tests address the marker by: the shared marker's own (mod-003 moved it). */
export { CATEGORY_MARKER_CLASS } from '../../../shared/ui/WorkTrackingMarks';

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
        <CategoryMarker code={category.code} />
        {category.label}
      </Box>
    </TableCell>
  );
}
