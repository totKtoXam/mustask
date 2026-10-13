import Avatar from '@mui/material/Avatar';
import { useTheme } from '@mui/material/styles';
import { initials } from '../../lib/initials';
import { ThinPersonIcon } from '../icons/ThinIcons';

export interface UserAvatarProps {
  /** The person's display name; the initials come from it (`shared/lib/initials`). */
  name: string;
  /** `menu` is the 32 px avatar of the user menu, `inline` (default) the 24 px one beside a name. */
  size?: 'menu' | 'inline';
}

/**
 * A person's avatar (tech-015 design § 5.4): MUI's `Avatar` with the initials at
 * the avatar type, 32 or 24 px. `aria-hidden` — the name beside it is what is
 * read. A name with no initials shows the thin person glyph. Its fill is the
 * theme's; the reference's is darker (the shell task, tech-019).
 */
export function UserAvatar({ name, size = 'inline' }: UserAvatarProps) {
  const theme = useTheme();
  const box = size === 'menu' ? theme.layout.avatarSize : theme.layout.mark.avatarInline;
  const letters = initials(name);
  return (
    <Avatar aria-hidden="true" sx={{ width: box, height: box, flexShrink: 0 }}>
      {letters ?? <ThinPersonIcon sx={{ fontSize: theme.layout.mark.personGlyph }} />}
    </Avatar>
  );
}
