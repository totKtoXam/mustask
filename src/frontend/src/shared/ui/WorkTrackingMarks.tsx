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
export function TypeMark({ appearance }: { appearance: string }) {
  const { icon: Icon, colour } = useTheme().workTracking.typeAppearance(appearance);
  return <Icon fontSize="small" aria-hidden="true" sx={{ color: colour, flexShrink: 0 }} />;
}

function markerSx(code: string) {
  return (theme: Theme) => {
    const colour = theme.workTracking.categoryColour(code);
    const size = theme.layout.categoryDot;
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
          border: `${theme.spacing(0.25)} solid ${colour}`,
        }
      : { ...shape, backgroundColor: colour };
  };
}

/**
 * A state category's marker (mod-015 § 2.3): a circle in the category's theme
 * colour, looked up by its code, a ring for `Removed`. `aria-hidden`: the
 * state name or category label beside it carries the meaning, so colour is
 * never the only carrier; an unknown code still gets a dot.
 */
export function CategoryMarker({ code }: { code: string }) {
  return (
    <Box
      component="span"
      aria-hidden="true"
      className={CATEGORY_MARKER_CLASS}
      sx={markerSx(code)}
    />
  );
}
