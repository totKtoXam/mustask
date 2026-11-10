import { useState } from 'react';
import Typography from '@mui/material/Typography';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { FormDialog } from '../../../shared/ui/FormDialog';
import { InlineSelectField } from '../../../shared/ui/fields/InlineSelectField';
import type { UserCandidate } from '../api/membershipApi';
import type { GrantRole, GrantRow, GrantScope } from '../api/grantsApi';
import { useChangeGrantRole, useGiveGrant, useRevokeGrant } from '../hooks/grantMutations';
import { placeGrantRefusal, type PlacedGrantRefusal } from '../lib/grantRefusals';
import {
  GRANT_ROLES,
  GRANT_ROLE_HELPERS,
  GRANT_ROLE_LABELS,
  LOWEST_ROLE,
  changeGrantDialog,
  giveDialog,
  revokeDialog,
} from '../lib/grantTexts';
import { plainName } from '../lib/userName';
import { UserLine } from './UserLine';
import { UserSearchField } from './UserSearchField';

interface Target {
  organizationId: string;
  scope: GrantScope;
  objectId: string;
  objectName: string;
}

const ROLE_OPTIONS = GRANT_ROLES.map((role) => ({ value: role, label: GRANT_ROLE_LABELS[role] }));

interface DialogOutcome {
  /** The grant is done (or already as wanted): the section says so. */
  onDone(message: string): void;
  /** The grant was changed or removed by someone else: the list says so. */
  onChanged(): void;
  onContextGone(): void;
  onClose(): void;
}

function useRefusal(outcome: DialogOutcome) {
  const [placed, setPlaced] = useState<PlacedGrantRefusal | null>(null);
  function refuse(error: unknown) {
    const next = placeGrantRefusal(error);
    if (next.changed) {
      outcome.onChanged();
      return;
    }
    if (next.contextGone) outcome.onContextGone();
    setPlaced(next);
  }
  return { placed, refuse, clear: () => setPlaced(null) };
}

/** «Выдача доступа» (design § 3): the user, the role (lowest preselected), refusals in place. */
export function GiveGrantDialog({ target, ...outcome }: { target: Target } & DialogOutcome) {
  const give = useGiveGrant(target.organizationId, target.scope, target.objectId);
  const [user, setUser] = useState<UserCandidate | null>(null);
  const [role, setRole] = useState<GrantRole>(LOWEST_ROLE);
  const { placed, refuse, clear } = useRefusal(outcome);

  async function submit() {
    if (user === null) return;
    try {
      await give.mutateAsync({ userId: user.id, role });
      outcome.onDone(giveDialog.given(user.displayName));
    } catch (error) {
      refuse(error);
    }
  }

  return (
    <FormDialog
      open
      title={giveDialog.title}
      submitLabel={giveDialog.submit}
      busy={give.isPending}
      error={placed?.alert}
      submitDisabled={user === null}
      onSubmit={() => void submit()}
      onClose={outcome.onClose}
    >
      <UserSearchField
        label={giveDialog.userLabel}
        scope={{
          kind: 'grant',
          organizationId: target.organizationId,
          grantScope: target.scope,
          objectId: target.objectId,
        }}
        value={user}
        onChange={(next) => {
          setUser(next);
          clear();
        }}
        error={placed?.field ?? undefined}
        disabled={give.isPending}
        autoFocus
      />
      <InlineSelectField<GrantRole>
        label={giveDialog.roleLabel}
        frame="always"
        value={role}
        onChange={(next) => next !== '' && setRole(next)}
        options={ROLE_OPTIONS}
        hint={GRANT_ROLE_HELPERS[target.scope][role]}
      />
    </FormDialog>
  );
}

/** «Изменение роли» (design § 3): the line with the user, the role, the helper of the chosen role. */
export function ChangeGrantRoleDialog({
  target,
  row,
  ...outcome
}: { target: Target; row: GrantRow } & DialogOutcome) {
  const change = useChangeGrantRole(target.organizationId, target.scope, target.objectId);
  const [role, setRole] = useState<GrantRole>(row.role);
  const { placed, refuse } = useRefusal(outcome);

  async function submit() {
    try {
      await change.mutateAsync({ userId: row.user.id, role });
      outcome.onDone(changeGrantDialog.changed(plainName(row.user)));
    } catch (error) {
      refuse(error);
    }
  }

  return (
    <FormDialog
      open
      title={changeGrantDialog.title}
      submitLabel={changeGrantDialog.submit}
      busy={change.isPending}
      error={placed?.alert ?? placed?.field}
      submitDisabled={role === row.role}
      onSubmit={() => void submit()}
      onClose={outcome.onClose}
    >
      <Typography variant="body2">
        {changeGrantDialog.user}
        <UserLine user={row.user} truncate={false} />
      </Typography>
      <InlineSelectField<GrantRole>
        label={giveDialog.roleLabel}
        frame="always"
        value={role}
        onChange={(next) => next !== '' && setRole(next)}
        options={ROLE_OPTIONS}
        hint={GRANT_ROLE_HELPERS[target.scope][role]}
      />
    </FormDialog>
  );
}

/** «Отозвать доступ?» (design § 3): two variants, the caller's own line is the second. */
export function RevokeGrantDialog({
  target,
  row,
  isSelf,
  ...outcome
}: { target: Target; row: GrantRow; isSelf: boolean } & DialogOutcome) {
  const revoke = useRevokeGrant(target.organizationId, target.scope, target.objectId);
  const { placed, refuse } = useRefusal(outcome);
  const role = GRANT_ROLE_LABELS[row.role];
  const name = plainName(row.user);

  async function confirm() {
    try {
      await revoke.mutateAsync(row.user.id);
      outcome.onDone(isSelf ? revokeDialog.doneSelf : revokeDialog.doneOther(name));
    } catch (error) {
      refuse(error);
    }
  }

  return (
    <ConfirmDialog
      open
      destructive
      cancelAutoFocus
      title={isSelf ? revokeDialog.titleSelf : revokeDialog.titleOther}
      confirmLabel={revokeDialog.confirm}
      busy={revoke.isPending}
      error={placed?.alert ?? placed?.field}
      onConfirm={() => void confirm()}
      onClose={outcome.onClose}
    >
      {isSelf
        ? revokeDialog.self(role, target.objectName)
        : revokeDialog.other(name, role, target.objectName)}
    </ConfirmDialog>
  );
}
