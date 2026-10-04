import { useTheme } from '@mui/material/styles';

/**
 * A type's appearance token drawn through the theme (design point 5, D-6):
 * the token's icon in its colour, before the type name. `aria-hidden`: the
 * name beside it carries the meaning, and the token's name is never text. An
 * unknown token gets the theme's neutral fallback and nothing throws.
 */
export function TypeMark({ appearance }: { appearance: string }) {
  const { icon: Icon, colour } = useTheme().workTracking.typeAppearance(appearance);
  return <Icon fontSize="small" aria-hidden="true" sx={{ color: colour, flexShrink: 0 }} />;
}
