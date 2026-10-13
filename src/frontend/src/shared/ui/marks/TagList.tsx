import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import { ThinAddIcon } from '../icons/ThinIcons';
import { Tag, type TagTone } from './Tag';

/** The kit's default name of the «+» that adds a tag. */
const DEFAULT_ADD_LABEL = 'Добавить тег';

export interface TagListItem {
  id: string;
  label: string;
  tone?: TagTone;
}

export interface TagListProps {
  tags: readonly TagListItem[];
  /** Given, every tag has its remove button; absent (a read-only host), none has. */
  onRemove?: (id: string) => void;
  /** Given, a ghost «+» ends the list — the place kept for the tags feature. */
  onAdd?: () => void;
  /** The «+»'s name; default «Добавить тег». */
  addLabel?: string;
}

/**
 * A wrapping list of tags (tech-015 design § 5.4): a `list`, the tags 8 px
 * apart, and — when `onAdd` is given — a trailing 24 px «+» on the neutral grey,
 * radius a half of the shape's. Nothing is drawn when there is nothing to show.
 */
export function TagList({ tags, onRemove, onAdd, addLabel = DEFAULT_ADD_LABEL }: TagListProps) {
  if (tags.length === 0 && !onAdd) return null;
  return (
    <Box
      component="ul"
      role="list"
      sx={{
        display: 'flex',
        flexWrap: 'wrap',
        alignItems: 'center',
        gap: 1,
        m: 0,
        p: 0,
        listStyle: 'none',
      }}
    >
      {tags.map((tag) => (
        <li key={tag.id}>
          <Tag
            label={tag.label}
            tone={tag.tone}
            onRemove={onRemove ? () => onRemove(tag.id) : undefined}
          />
        </li>
      ))}
      {onAdd && (
        <li>
          <IconButton
            aria-label={addLabel}
            onClick={() => onAdd()}
            sx={(theme) => ({
              boxSizing: 'border-box',
              width: theme.layout.iconAction.row.box,
              height: theme.layout.iconAction.row.box,
              padding: 0,
              fontSize: theme.layout.iconAction.row.glyph,
              color: theme.palette.text.primary,
              backgroundColor: theme.palette.shell.buttonNeutral,
              borderRadius: `${Number(theme.shape.borderRadius) / 2}px`,
              '&:hover': { backgroundColor: theme.palette.shell.buttonNeutralHover },
            })}
          >
            <ThinAddIcon fontSize="inherit" aria-hidden="true" />
          </IconButton>
        </li>
      )}
    </Box>
  );
}
