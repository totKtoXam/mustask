import type { Ref } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import ManageAccountsOutlinedIcon from '@mui/icons-material/ManageAccountsOutlined';
import PersonRemoveOutlinedIcon from '@mui/icons-material/PersonRemoveOutlined';
import { formatDateTime } from '../../../shared/lib/format';
import type { Member, UserRef } from '../api/membershipApi';
import { MEMBERS_COLUMNS, membersPage } from '../lib/memberTexts';
import { ROLE_LABELS } from '../lib/roles';
import { plainName } from '../lib/userName';
import { RowActionsMenu, type RowAction } from './RowActionsMenu';
import { TruncatedName } from './TruncatedName';
import { UserLine } from './UserLine';

/** Who did it and when, as «<имя>, <дата и время>» (design § 3.2). */
function Stamp({ by, at }: { by: UserRef; at: string }) {
  return (
    <>
      <UserLine user={by} truncate={false} />
      <Box component="span" sx={{ whiteSpace: 'nowrap' }}>
        {`, ${formatDateTime(at)}`}
      </Box>
    </>
  );
}

export interface MembersTableProps {
  members: readonly Member[];
  /**
   * The actions column and the row menus exist for the holder of
   * `organizations.membership.write` only (DC-2): with no administrator the
   * column is not drawn.
   */
  canWrite: boolean;
  onChangeRole(member: Member): void;
  onRemove(member: Member): void;
  /** A handle on a row's «Действия» button, for the focus after a removal. */
  actionsRef(userId: string): Ref<HTMLButtonElement>;
}

/**
 * «Участники организации» (design § 3.1, § 3.2): a plain table — no filter,
 * no paging, rows in the server's order (by name, unknown users last). The
 * table scrolls sideways inside its own paper at narrow widths and never the
 * page (design § 11): it has a floor width, names truncate with a tooltip and
 * dates and e-mails do not wrap.
 */
export function MembersTable({
  members,
  canWrite,
  onChangeRole,
  onRemove,
  actionsRef,
}: MembersTableProps) {
  function rowActions(member: Member): RowAction[] {
    return [
      {
        label: membersPage.changeRole,
        icon: <ManageAccountsOutlinedIcon fontSize="small" />,
        onClick: () => onChangeRole(member),
      },
      {
        label: membersPage.removeFromOrganization,
        icon: <PersonRemoveOutlinedIcon fontSize="small" />,
        onClick: () => onRemove(member),
        destructive: true,
      },
    ];
  }

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table
        size="small"
        aria-label={membersPage.tableLabel}
        sx={(theme) => ({ minWidth: theme.spacing(90) })}
      >
        <TableHead>
          <TableRow>
            {MEMBERS_COLUMNS.map((column) => (
              <TableCell key={column}>{column}</TableCell>
            ))}
            {canWrite && <TableCell />}
          </TableRow>
        </TableHead>
        <TableBody>
          {members.map((member) => (
            <TableRow key={member.user.id} hover>
              <TableCell sx={{ maxWidth: 0, width: '24%' }}>
                <UserLine user={member.user} />
              </TableCell>
              <TableCell sx={{ maxWidth: 0, width: '22%' }}>
                {member.user.email ? <TruncatedName name={member.user.email} /> : null}
              </TableCell>
              <TableCell sx={{ whiteSpace: 'nowrap' }}>{ROLE_LABELS[member.role]}</TableCell>
              <TableCell>
                <Stamp by={member.addedBy} at={member.addedAt} />
              </TableCell>
              <TableCell>
                {member.roleChangedBy !== null && member.roleChangedAt !== null ? (
                  <Stamp by={member.roleChangedBy} at={member.roleChangedAt} />
                ) : (
                  <>
                    <span aria-hidden="true">—</span>
                    <Box component="span" sx={(theme) => theme.mixins.visuallyHidden}>
                      {membersPage.roleNeverChanged}
                    </Box>
                  </>
                )}
              </TableCell>
              {canWrite && (
                <TableCell align="right">
                  <RowActionsMenu
                    name={plainName(member.user)}
                    actions={rowActions(member)}
                    buttonRef={actionsRef(member.user.id)}
                  />
                </TableCell>
              )}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
