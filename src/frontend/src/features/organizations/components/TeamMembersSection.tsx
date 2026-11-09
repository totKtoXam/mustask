import { useEffect, useEffectEvent, useId, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import AddIcon from '@mui/icons-material/Add';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import PersonRemoveOutlinedIcon from '@mui/icons-material/PersonRemoveOutlined';
import { useAccessCheck } from '../../../shared/lib/access';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { DataTable, type DataColumn } from '../../../shared/ui/table/DataTable';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { StateFrame } from '../../../shared/ui/StateFrame';
import type { Team } from '../api/organizationsApi';
import type { UserRef } from '../api/membershipApi';
import { useAddTeamMember, useRemoveTeamMember } from '../hooks/mutations';
import { useGrants, useTeamMembers } from '../hooks/queries';
import { GRANT_ROLE_LABELS, grantsSection } from '../lib/grantTexts';
import { useRowFocus } from '../hooks/useRowFocus';
import { memberNotices, teamMembersSection } from '../lib/memberTexts';
import { placeMembershipRefusal } from '../lib/membershipRefusals';
import { notFoundLevel } from '../lib/notFoundLevel';
import { plainName } from '../lib/userName';
import { organizationsPermissions } from '../permissions';
import { AddTeamMemberDialog } from './AddTeamMemberDialog';
import { useNotify } from './notice';
import { RowActionsMenu } from './RowActionsMenu';
import { TruncatedName } from './TruncatedName';
import { UserLine } from './UserLine';

export interface TeamMembersSectionProps {
  organizationId: string;
  team: Pick<Team, 'id' | 'name'>;
  /**
   * The team of the address turned out to be gone (mod-002 § 12): the
   * page reads its own entity again and shows the frame of the level.
   */
  onContextGone(): void;
}

/**
 * «Участники команды» on the team page (US-3; mod-016 design § 4): the team's
 * members under the facts, a plain table of «Имя» and «Почта» in the server's
 * order, with the marks of design § 3.3. It takes the team and its
 * organization and builds no address of its own, so it follows the team page
 * wherever feat-028 puts it. The holder of `organizations.team.write` adds
 * participants through «Добавить в команду» and removes one at once with
 * «Исключить из команды», with no confirmation (DC-6: it grants and removes no
 * access, and adding again undoes it); everyone with the read code sees the
 * same table without those controls.
 *
 * States: the section's header stays through loading and an error (the facts
 * above load on their own); an empty team is the canon frame under the
 * header, one level down (`h3`), with the administrator's hint and action or
 * the others' hint alone.
 */
export function TeamMembersSection({
  organizationId,
  team,
  onContextGone,
}: TeamMembersSectionProps) {
  const headingId = useId();
  const query = useTeamMembers(organizationId, team.id);
  // The role a member holds on the team by a grant (mod-019 design § 3); none is a dash.
  const grants = useGrants(organizationId, 'team', team.id);
  const roleOf = new Map((grants.data?.items ?? []).map((row) => [row.user.id, row.role]));
  const add = useAddTeamMember(organizationId, team.id);
  const remove = useRemoveTeamMember(organizationId, team.id);
  const access = useAccessCheck();
  const canWrite = access(organizationsPermissions.teamWrite, 'organization');
  const notify = useNotify();
  const [adding, setAdding] = useState(false);
  const [busyIds, setBusyIds] = useState<ReadonlySet<string>>(new Set());
  const addButton = useRef<HTMLButtonElement>(null);
  const focusAddButton = useRef(false);
  const focus = useRowFocus(
    query.data?.map((user) => user.id),
    () => addButton.current,
  );

  // An added participant: focus returns to the section's «Добавить в команду»
  // once the dialog is gone — the frame's own action, the opener when the team
  // was empty, goes with the frame (design § 4.4).
  useEffect(() => {
    if (focusAddButton.current && !adding) {
      focusAddButton.current = false;
      addButton.current?.focus();
    }
  }, [adding]);

  // The page's own entity is gone: it reads itself again and shows its frame
  // (design § 9, last row); this section keeps its banner meanwhile.
  const gone = query.isError && notFoundLevel(query.error) !== null;
  const reportGone = useEffectEvent(onContextGone);
  useEffect(() => {
    if (gone) {
      reportGone();
    }
  }, [gone]);

  async function removeFromTeam(user: UserRef) {
    const neighbours = focus.neighboursOf(user.id);
    setBusyIds((current) => new Set(current).add(user.id));
    try {
      await remove.mutateAsync(user.id);
      focus.afterRemoval(user.id, neighbours);
      notify(memberNotices.removedFromTeam(plainName(user), team.name));
    } catch (error) {
      const placed = placeMembershipRefusal(error, 'teamRemove');
      focus.afterFailure(user.id);
      if (placed.notice !== null) {
        notify(placed.notice, 'error');
      }
      if (placed.reread) {
        void query.refetch();
      }
      if (placed.contextGone) {
        onContextGone();
      }
    } finally {
      setBusyIds((current) => {
        const next = new Set(current);
        next.delete(user.id);
        return next;
      });
    }
  }

  const loading = query.isPending || (query.isError && query.isFetching);
  const tableColumns: DataColumn<UserRef>[] = [
    {
      key: 'name',
      header: teamMembersSection.columns.name,
      width: '40%',
      render: (user) => <UserLine user={user} />,
    },
    {
      key: 'email',
      header: teamMembersSection.columns.email,
      width: '40%',
      render: (user) => (user.email ? <TruncatedName name={user.email} /> : null),
    },
    {
      key: 'role',
      header: grantsSection.teamRoleColumn,
      render: (user) =>
        roleOf.has(user.id) ? (
          GRANT_ROLE_LABELS[roleOf.get(user.id)!]
        ) : (
          <span aria-label={grantsSection.noTeamRoleForEar}>{grantsSection.noTeamRole}</span>
        ),
    },
    ...(canWrite
      ? [
          {
            key: 'actions',
            header: '',
            align: 'right' as const,
            width: 48,
            render: (user: UserRef) => (
              <RowActionsMenu
                name={plainName(user)}
                actions={[
                  {
                    label: teamMembersSection.removeFromTeam,
                    icon: <PersonRemoveOutlinedIcon fontSize="small" />,
                    onClick: () => void removeFromTeam(user),
                  },
                ]}
                busy={busyIds.has(user.id)}
                buttonRef={focus.buttonRef(user.id)}
              />
            ),
          },
        ]
      : []),
  ];
  const columns: string[] = [
    teamMembersSection.columns.name,
    teamMembersSection.columns.email,
    grantsSection.teamRoleColumn,
  ];

  return (
    <Box component="section" aria-labelledby={headingId} sx={{ mt: 4 }}>
      <Stack
        direction="row"
        spacing={2}
        sx={{ alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', mb: 1 }}
      >
        <Typography variant="h2" component="h2" id={headingId}>
          {teamMembersSection.title}
        </Typography>
        {canWrite && (
          <Button
            ref={addButton}
            variant="text"
            startIcon={<AddIcon />}
            onClick={() => setAdding(true)}
          >
            {teamMembersSection.add}
          </Button>
        )}
      </Stack>
      {loading ? (
        <RegistrySkeleton columns={canWrite ? [...columns, ''] : columns} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : query.data.length === 0 ? (
        <StateFrame
          icon={InboxOutlinedIcon}
          headingLevel={3}
          heading={teamMembersSection.empty.heading}
          hint={canWrite ? teamMembersSection.empty.adminHint : teamMembersSection.empty.otherHint}
          action={
            canWrite
              ? {
                  label: teamMembersSection.add,
                  icon: <AddIcon />,
                  onClick: () => setAdding(true),
                }
              : undefined
          }
        />
      ) : (
        <DataTable
          aria-label={teamMembersSection.title}
          columns={tableColumns}
          rows={query.data}
          rowKey={(user) => user.id}
        />
      )}
      <RequiresOrganizationCode code={organizationsPermissions.teamWrite}>
        {adding && (
          <AddTeamMemberDialog
            organizationId={organizationId}
            teamId={team.id}
            submit={(userId) => add.mutateAsync(userId)}
            onSucceeded={(user) => {
              focusAddButton.current = true;
              setAdding(false);
              notify(memberNotices.addedToTeam(plainName(user), team.name));
            }}
            onReread={() => void query.refetch()}
            onContextGone={onContextGone}
            onClose={() => setAdding(false)}
          />
        )}
      </RequiresOrganizationCode>
    </Box>
  );
}
