import Box from '@mui/material/Box';
import { useTheme } from '@mui/material/styles';
import { ThinPersonIcon } from '../icons/ThinIcons';
import { UserAvatar } from './UserAvatar';

/** The kit's default text of an empty person. */
const DEFAULT_EMPTY_LABEL = 'Не назначен';

export interface PersonLabelProps {
  /** The person's display name, or `null` for nobody. */
  name: string | null;
  /** Default `inline` (24 px); `menu` is the 32 px avatar. */
  size?: 'inline' | 'menu';
  /**
   * What stands for nobody; default «Не назначен». A host that has an id and no
   * name passes its own words, e.g. «Неизвестный пользователь».
   */
  emptyLabel?: string;
}

/**
 * A person: the avatar and the name, or — for nobody — the thin person glyph
 * (`layout.mark.personGlyph`, centred in the avatar's slot) and the empty text in
 * `text.secondary` (tech-015 design § 5.4).
 */
export function PersonLabel({
  name,
  size = 'inline',
  emptyLabel = DEFAULT_EMPTY_LABEL,
}: PersonLabelProps) {
  const theme = useTheme();
  const slot = size === 'menu' ? theme.layout.avatarSize : theme.layout.mark.avatarInline;
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
      {name === null ? (
        <>
          <Box
            component="span"
            aria-hidden="true"
            sx={{
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: slot,
              height: slot,
              flexShrink: 0,
              color: 'text.secondary',
            }}
          >
            <ThinPersonIcon sx={{ fontSize: theme.layout.mark.personGlyph }} />
          </Box>
          <Box component="span" sx={{ color: 'text.secondary' }}>
            {emptyLabel}
          </Box>
        </>
      ) : (
        <>
          <UserAvatar name={name} size={size} />
          <Box
            component="span"
            sx={{ minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
          >
            {name}
          </Box>
        </>
      )}
    </Box>
  );
}
