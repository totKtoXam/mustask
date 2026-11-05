import Box from '@mui/material/Box';
import { useTheme } from '@mui/material/styles';
import { CategoryMarker } from '../WorkTrackingMarks';

export interface StateLabelProps {
  /** doc-007's state-category code — never a state name or a label. */
  categoryCode: string;
  /** The state's name as the server sent it, drawn as text. */
  name: string;
  /** The dot's size: `row` (default, 8 px) or `field` (10 px, the card's band). */
  size?: 'row' | 'field';
  /** One line with an ellipsis; the full name stays in the native `title`. */
  clip?: boolean;
}

/**
 * A state: the category's dot, `layout.mark.nameGap` and the state's name
 * (tech-015 design § 5.4). The dot repeats nothing — it is decoration beside
 * the name, which carries the meaning: a state is never shown as a coloured chip.
 */
export function StateLabel({ categoryCode, name, size = 'row', clip = false }: StateLabelProps) {
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
      <CategoryMarker code={categoryCode} size={size} />
      <Box
        component="span"
        title={clip ? name : undefined}
        sx={
          clip
            ? { minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }
            : undefined
        }
      >
        {name}
      </Box>
    </Box>
  );
}
