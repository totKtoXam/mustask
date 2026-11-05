import Box from '@mui/material/Box';
import { useTheme } from '@mui/material/styles';
import { TypeMark } from '../WorkTrackingMarks';

export interface TypeLabelProps {
  /** The type's stored appearance token — never a type key or a name. */
  appearance: string;
  /** The type's name, drawn as text: the mark is decoration, the name is what is read. */
  name: string;
  /**
   * `default` is the 20 px icon at the body type (a form, a picker); `compact` the
   * table's 16 px icon at the table-cell type.
   */
  size?: 'default' | 'compact';
  /** On a selected row the icon takes `primary.dark` (6.6:1 on `rowSelected`). */
  selected?: boolean;
  /**
   * The card's type line: the compact icon, the caption type, capitals **by CSS**
   * (the DOM text and the accessible name keep their case), `primary.dark`.
   */
  eyebrow?: boolean;
}

/**
 * A work item type: its icon in its colour, `layout.mark.nameGap` and its name
 * (tech-015 design § 5.4). Colour is never the only carrier — the name always
 * stands beside the icon, and the eyebrow names the type in text.
 */
export function TypeLabel({
  appearance,
  name,
  size = 'default',
  selected = false,
  eyebrow = false,
}: TypeLabelProps) {
  const theme = useTheme();
  const compact = size === 'compact' || eyebrow;
  return (
    <Box
      component="span"
      sx={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: `${theme.layout.mark.nameGap}px`,
        minWidth: 0,
        maxWidth: '100%',
        ...(eyebrow
          ? {
              ...theme.typography.caption,
              color: theme.palette.primary.dark,
              textTransform: 'uppercase',
              letterSpacing: 'normal',
            }
          : size === 'compact'
            ? theme.typography.tableCell
            : theme.typography.body2),
      }}
    >
      <TypeMark
        appearance={appearance}
        compact={compact}
        colour={selected ? theme.palette.primary.dark : undefined}
      />
      <Box
        component="span"
        sx={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
      >
        {name}
      </Box>
    </Box>
  );
}
