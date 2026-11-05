import Chip from '@mui/material/Chip';
import type { Theme } from '@mui/material/styles';
import { IconAction } from '../actions/IconAction';
import { ThinCloseIcon } from '../icons/ThinIcons';

export type TagTone = 'neutral' | 'accent' | 'success' | 'warning';

export interface TagProps {
  label: string;
  /**
   * `neutral` (default) the grey pill, `accent` the pale-blue one of the backlog's
   * tags; `success` and `warning` carry a registry status («Активен», «изменилось»).
   */
  tone?: TagTone;
  /** `filled` (default) or a 1 px outline in the tone's colour. */
  variant?: 'filled' | 'outlined';
  /**
   * Given, the tag has a remove button named «Удалить тег «<label>»». A tag of a
   * read-only host passes none.
   */
  onRemove?: () => void;
  /**
   * The remove button's name from the tag's label; default «Удалить тег «<label>»». A host
   * whose tags are not "tags" says its own (the multiple picker: «Убрать «Alpha»»).
   */
  removeLabel?: (label: string) => string;
}

/** The kit’s text of a tag’s remove button. */
function removeTagLabel(label: string): string {
  return `Удалить тег «${label}»`;
}

/**
 * A tag or a status pill (tech-015 design § 5.4): MUI's small `Chip`, a full pill,
 * the label at the table-cell type. It never carries a state — a state is a dot
 * and a name (`StateLabel`).
 */
export function Tag({
  label,
  tone = 'neutral',
  variant = 'filled',
  onRemove,
  removeLabel = removeTagLabel,
}: TagProps) {
  const outlined = variant === 'outlined';
  return (
    <Chip
      size="small"
      variant={outlined ? 'outlined' : 'filled'}
      color={
        tone === 'success' || tone === 'warning' ? tone : tone === 'accent' ? 'primary' : 'default'
      }
      // The remove button is part of the label, not Chip's `onDelete`: that would make
      // the whole pill a button with a button inside it.
      label={
        onRemove ? (
          <>
            {label}
            <IconAction
              label={removeLabel(label)}
              icon={<ThinCloseIcon />}
              size="row"
              tooltip={false}
              onClick={() => onRemove()}
            />
          </>
        ) : (
          label
        )
      }
      sx={(theme) => tagSx(theme, tone, outlined, onRemove !== undefined)}
    />
  );
}

function tagSx(theme: Theme, tone: TagTone, outlined: boolean, removable: boolean) {
  const neutral = tone === 'neutral';
  const accent = tone === 'accent';
  return {
    maxWidth: '100%',
    '& .MuiChip-label': {
      ...theme.typography.tableCell,
      display: 'inline-flex',
      alignItems: 'center',
      gap: `${theme.layout.mark.nameGap / 2}px`,
      paddingRight: removable ? 0 : undefined,
    },
    ...(outlined
      ? {
          color: neutral
            ? theme.palette.text.primary
            : accent
              ? theme.palette.primary.dark
              : undefined,
          borderColor: neutral ? theme.palette.shell.inputBorder : undefined,
        }
      : neutral
        ? { backgroundColor: theme.palette.shell.buttonNeutral, color: theme.palette.text.primary }
        : accent
          ? { backgroundColor: theme.palette.shell.rowSelected, color: theme.palette.primary.dark }
          : {}),
  };
}
