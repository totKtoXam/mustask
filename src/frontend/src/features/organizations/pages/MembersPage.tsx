import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import Box from '@mui/material/Box';
import AddIcon from '@mui/icons-material/Add';
import { focusMainRegion } from '../../../shared/lib/mainRegion';
import { useAccessCheck } from '../../../shared/lib/access';
import { useWorkContext } from '../../../shared/lib/workContext';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { RegistrySkeleton } from '../../../shared/ui/RegistrySkeleton';
import { RequiresOrganizationCode } from '../../../shared/ui/RequiresOrganizationCode';
import { NotFound } from '../../../shared/ui/StateFrame';
import { useAuth } from '../../auth/AuthContext';
import { useForgetAccessOnLeave } from '../access/useForgetAccessOnLeave';
import type { Member } from '../api/membershipApi';
import { organizationsKeys } from '../api/queryKeys';
import { AddMemberDialog } from '../components/AddMemberDialog';
import { ChangeRoleDialog } from '../components/ChangeRoleDialog';
import { MembersTable } from '../components/MembersTable';
import { useNotify } from '../components/notice';
import { RemoveMemberDialog } from '../components/RemoveMemberDialog';
import { useAddMember, useChangeMemberRole, useRemoveMember } from '../hooks/mutations';
import { useMembers, useOrganization } from '../hooks/queries';
import { useRowFocus } from '../hooks/useRowFocus';
import { MEMBERS_COLUMNS, membersPage, memberNotices } from '../lib/memberTexts';
import { notFoundLevel } from '../lib/notFoundLevel';
import { plainName } from '../lib/userName';
import { ORGANIZATIONS_PATH } from '../paths';
import { organizationsPermissions } from '../permissions';

type MembersDialog =
  { kind: 'add' } | { kind: 'role'; member: Member } | { kind: 'remove'; member: Member } | null;

/**
 * «Участники» (US-1; mod-016 design § 3): the organization's members with
 * their roles, who added them and who last changed the role. A plain table
 * with no filter, no paging and no empty state — an organization always has
 * its creator (DC-3). An administrator (`organizations.membership.write`)
 * adds members, changes roles and removes members through three dialogs;
 * everyone with the read code sees the same table without those controls.
 * The page is mounted by the organization gate only once the codes are read,
 * so its controls never flash and «Доступ ограничен» is the gate's.
 */
export function MembersPage() {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const query = useMembers(organizationId);
  const add = useAddMember(organizationId);
  const { subject } = useAuth();
  const changeRole = useChangeMemberRole(organizationId, subject);
  const remove = useRemoveMember(organizationId, subject);
  const access = useAccessCheck();
  const canWrite = access(organizationsPermissions.membershipWrite, 'organization');
  const context = useWorkContext();
  const client = useQueryClient();
  const navigate = useNavigate();
  const notify = useNotify();
  const { leave } = useForgetAccessOnLeave(organizationId, { focusMain: true });
  const [dialog, setDialog] = useState<MembersDialog>(null);
  const pageRef = useRef<HTMLDivElement>(null);
  const steppedDown = useRef(false);

  // The organization's name, for the removal's texts: the switcher lists every
  // organization the user is in; the page reads it only when it does not.
  const listed = context.organizations.find((entry) => entry.id === organizationId)?.name;
  const organization = useOrganization(organizationId, { enabled: listed === undefined });
  const organizationName = listed ?? organization.data?.name ?? '';

  const focus = useRowFocus(
    query.data?.map((member) => member.user.id),
    () =>
      Array.from(pageRef.current?.querySelectorAll('button') ?? []).find(
        (button) => button.textContent === membersPage.add,
      ),
  );

  // The caller stepped down from administrator: the codes are read again, the
  // controls the role no longer allows are gone with no reload, and the
  // control that had focus is among them — focus goes to the main region once
  // the dialog has closed (design § 3.6, § 2.2).
  useEffect(() => {
    if (steppedDown.current && dialog === null && !canWrite) {
      steppedDown.current = false;
      focusMainRegion();
    }
  }, [dialog, canWrite]);

  const level = query.isError ? notFoundLevel(query.error) : null;
  if (level) {
    return <NotFound level={level} />;
  }

  const rereadMembers = () =>
    void client.invalidateQueries({ queryKey: organizationsKeys.members(organizationId) });
  const closeDialog = () => setDialog(null);
  const loading = query.isPending || (query.isError && query.isFetching);

  return (
    <Box ref={pageRef}>
      <PageHeader
        title={membersPage.title}
        primaryAction={
          canWrite
            ? {
                label: membersPage.add,
                icon: <AddIcon />,
                onClick: () => setDialog({ kind: 'add' }),
              }
            : undefined
        }
      />
      {loading ? (
        <RegistrySkeleton columns={canWrite ? [...MEMBERS_COLUMNS, ''] : [...MEMBERS_COLUMNS]} />
      ) : query.isError ? (
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      ) : (
        <MembersTable
          members={query.data}
          canWrite={canWrite}
          onChangeRole={(member) => setDialog({ kind: 'role', member })}
          onRemove={(member) => setDialog({ kind: 'remove', member })}
          actionsRef={focus.buttonRef}
        />
      )}
      <RequiresOrganizationCode code={organizationsPermissions.membershipWrite}>
        {dialog?.kind === 'add' && (
          <AddMemberDialog
            organizationId={organizationId}
            submit={(body) => add.mutateAsync(body)}
            onSucceeded={(member) => {
              setDialog(null);
              notify(memberNotices.added(plainName(member.user)));
            }}
            onReread={rereadMembers}
            onClose={closeDialog}
          />
        )}
        {dialog?.kind === 'role' && (
          <ChangeRoleDialog
            member={dialog.member}
            isSelf={dialog.member.user.id === subject}
            submit={(role) => changeRole.mutateAsync({ userId: dialog.member.user.id, role })}
            onSucceeded={(changed) => {
              steppedDown.current = dialog.member.user.id === subject && changed.role !== 'admin';
              setDialog(null);
              notify(memberNotices.roleChanged(plainName(dialog.member.user)));
            }}
            onReread={rereadMembers}
            onClose={closeDialog}
          />
        )}
        {dialog?.kind === 'remove' && (
          <RemoveMemberDialog
            member={dialog.member}
            organizationName={organizationName}
            isSelf={dialog.member.user.id === subject}
            action={async () => {
              const member = dialog.member;
              const neighbours = focus.neighboursOf(member.user.id);
              // DC-7, judged before the switcher sees the change: removing
              // oneself from the switcher's organization while another is
              // listed makes the switcher fall back, and its notice is the
              // only one.
              const fallsBack =
                member.user.id === subject &&
                context.selection.organizationId === organizationId &&
                context.organizations.some((entry) => entry.id !== organizationId);
              await remove.mutateAsync(member.user.id);
              return { fallsBack, neighbours };
            }}
            onSucceeded={({ fallsBack, neighbours }) => {
              const { member } = dialog;
              setDialog(null);
              if (member.user.id === subject) {
                // The caller is out: the page is left, the access entry goes
                // with it, and focus lands on the main region.
                leave();
                navigate(ORGANIZATIONS_PATH);
                if (!fallsBack) {
                  notify(memberNotices.removedSelf(organizationName));
                }
                return;
              }
              focus.afterRemoval(member.user.id, neighbours);
              notify(memberNotices.removed(plainName(member.user)));
            }}
            onReread={rereadMembers}
            onClose={closeDialog}
          />
        )}
      </RequiresOrganizationCode>
    </Box>
  );
}
