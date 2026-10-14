import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import type { UserRef } from '../api/membershipApi';
import { INACTIVE_MARK, UNKNOWN_USER, plainName } from '../lib/userName';
import { TruncatedName } from './TruncatedName';

/**
 * A user wherever this task draws one — «Имя», «Добавлен», «Роль изменена»,
 * the team members, the line «Пользователь: <имя>» (mod-016 design § 3.3,
 * BR-M1-17, BR-M1-32):
 *
 * - an active user is their display name;
 * - an inactive one (the Keycloak account is disabled or deleted) is the name
 *   and then the mark «неактивен» — text in an outlined chip, never colour
 *   alone, so it is read aloud after the name;
 * - one the projection does not know is «Неизвестный пользователь» in
 *   secondary text.
 *
 * `truncate` (the default) keeps the name to one line with its full text in a
 * tooltip, for a cell of its own; without it the name and mark flow inline,
 * so a comma and a date can follow them in «Добавлен».
 */
export function UserLine({ user, truncate = true }: { user: UserRef; truncate?: boolean }) {
  if (user.state === 'unknown') {
    return (
      <Box component="span" sx={{ color: 'text.secondary' }}>
        {UNKNOWN_USER}
      </Box>
    );
  }

  const name = plainName(user);
  const mark =
    user.state === 'inactive' ? (
      <Chip
        size="small"
        variant="outlined"
        label={INACTIVE_MARK}
        sx={{ flexShrink: 0, ml: truncate ? 0 : 1 }}
      />
    ) : null;

  if (!truncate) {
    return (
      <Box component="span">
        {name}
        {mark}
      </Box>
    );
  }
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
      <Box sx={{ minWidth: 0 }}>
        <TruncatedName name={name} />
      </Box>
      {mark}
    </Box>
  );
}
