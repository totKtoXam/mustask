import Box from '@mui/material/Box';
import { useTheme } from '@mui/material/styles';
import { TruncatedText } from '../text/TruncatedText';

export interface ColorLabelProps {
  /** The stored palette key of a department, team or project (doc-016 BR-TD-20) — never a name. */
  colour: string | null | undefined;
  /** The object's name, drawn as text: the dot is decoration, the name is what is read. */
  name: string;
  /** `department` is the 10 px dot, `child` the 8 px dot of a team or a project. */
  size?: 'department' | 'child';
  /** Given, the name is a router link (as `TruncatedText`'s `to`). */
  to?: string;
  /** With `to`: the router state the link carries. */
  linkState?: unknown;
}

/**
 * The dot alone (doc-016 design § 7.1), for a place that holds one icon, such as a page title. It is
 * `aria-hidden`: the name is always beside it.
 */
export function ColorDot({
  colour,
  size = 'child',
}: {
  colour: string;
  size?: 'department' | 'child';
}) {
  const theme = useTheme();
  const dot = size === 'department' ? theme.layout.mark.categoryDotField : theme.layout.categoryDot;
  return (
    <Box
      component="span"
      aria-hidden="true"
      data-testid="colour-dot"
      sx={{
        display: 'inline-block',
        width: dot,
        height: dot,
        borderRadius: '50%',
        flexShrink: 0,
        backgroundColor: theme.workTracking.objectColour(colour),
      }}
    />
  );
}

/**
 * An object's name with its colour as a dot before it (doc-016 design § 7.1). The dot is
 * `aria-hidden` and the name always stands beside it: colour is never the only carrier. A missing
 * colour draws the name alone.
 */
export function ColorLabel({ colour, name, size = 'child', to, linkState }: ColorLabelProps) {
  const theme = useTheme();
  return (
    <Box
      component="span"
      sx={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: `${theme.layout.mark.nameGap}px`,
        minWidth: 0,
        maxWidth: '100%',
      }}
    >
      {colour ? <ColorDot colour={colour} size={size} /> : null}
      <TruncatedText to={to} linkState={linkState} look="link">
        {name}
      </TruncatedText>
    </Box>
  );
}
