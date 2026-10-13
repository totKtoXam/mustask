import Box from '@mui/material/Box';
import { useTheme, type Theme } from '@mui/material/styles';

// The two work-tracking marks of mod-015 design § 2.3: a type's icon and a
// state's category marker. mod-001's process screen drew them first; mod-003's
// list, dialog and form draw them too, so they live here (frontend.md "Shared
// patterns": two slices need them). Both read the theme's lookups only.

/** The removed category is drawn as a ring, every other code as a dot (mod-015 design § 2.3). */
const REMOVED_CATEGORY = 'Removed';

/** Class name the tests address a category marker by. */
export const CATEGORY_MARKER_CLASS = 'WorkTrackingCategoryMarker';

/**
 * A type's appearance token drawn through the theme (mod-015 § 2.3, mod-001
 * D-6): the token's icon in its colour, before the type name or identifier.
 * `aria-hidden`: the text beside it carries the meaning, and the token's name
 * is never text. An unknown token gets the theme's neutral fallback and
 * nothing throws.
 */
export function TypeMark({
  appearance,
  compact = false,
  colour: override,
}: {
  appearance: string;
  /** The backlog's 16 px icon (feat-027 design § 4.4) instead of the 20 px one. */
  compact?: boolean;
  /** Another colour than the type's own — the selected row's dark blue (feat-027 design § 4.4). */
  colour?: string;
}) {
  const theme = useTheme();
  const { icon: Icon, colour } = theme.workTracking.typeAppearance(appearance);
  return (
    <Icon
      fontSize="inherit"
      aria-hidden="true"
      sx={{
        color: override ?? colour,
        flexShrink: 0,
        fontSize: compact ? theme.layout.mark.typeIconCompact : theme.layout.mark.typeIcon,
      }}
    />
  );
}

function markerSx(code: string, field: boolean) {
  return (theme: Theme) => {
    const colour = theme.workTracking.categoryColour(code);
    const size = field ? theme.layout.mark.categoryDotField : theme.layout.categoryDot;
    const shape = {
      display: 'inline-block',
      width: size,
      height: size,
      borderRadius: '50%',
      flexShrink: 0,
    };
    return code === REMOVED_CATEGORY
      ? {
          ...shape,
          boxSizing: 'border-box' as const,
          border: `${theme.layout.frame.thick}px solid ${colour}`,
        }
      : { ...shape, backgroundColor: colour };
  };
}

/**
 * A state category's marker (mod-015 § 2.3): a circle in the category's theme
 * colour, looked up by its code, a ring for `Removed`. `aria-hidden`: the
 * state name or category label beside it carries the meaning, so colour is
 * never the only carrier; an unknown code still gets a dot.
 *
 * `size`: `row` (default) is the table's 8 px dot, `field` the 10 px dot of the
 * card's band (tech-015 design § 5.4).
 */
export function CategoryMarker({ code, size = 'row' }: { code: string; size?: 'row' | 'field' }) {
  return (
    <Box
      component="span"
      aria-hidden="true"
      className={CATEGORY_MARKER_CLASS}
      sx={markerSx(code, size === 'field')}
    />
  );
}
