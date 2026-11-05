import { useState } from 'react';
import MenuItem from '@mui/material/MenuItem';
import TextField from '@mui/material/TextField';
import { FormDialog } from '../../../shared/ui/FormDialog';
import type { Member, UserCandidate } from '../api/membershipApi';
import { addMemberDialog } from '../lib/memberTexts';
import { placeMembershipRefusal } from '../lib/membershipRefusals';
import {
  ORGANIZATION_ROLES,
  ROLE_DESCRIPTIONS,
  ROLE_LABELS,
  type OrganizationRole,
} from '../lib/roles';
import { UserSearchField } from './UserSearchField';

export interface AddMemberDialogProps {
  organizationId: string;
  /** Sends the add; resolves with the new membership. */
  submit(body: { userId: string; role: OrganizationRole }): Promise<Member>;
  onSucceeded(member: Member): void;
  /** A refusal that says the list is out of date: read it again (design § 3.5). */
  onReread(): void;
  onClose(): void;
}

/**
 * «Добавление участника» (US-1, design § 3.5): «Пользователь» — a search by
 * name or e-mail, focused — and «Роль», «Участник» by default, whose helper
 * says what the role may do. «Добавить» stays disabled until a user is
 * chosen. The server's refusals of the user land on the user field with the
 * chosen user kept, so the administrator can pick another; any other refusal
 * is the dialog's `Alert`. Mounted per opening.
 */
export function AddMemberDialog({
  organizationId,
  submit,
  onSucceeded,
  onReread,
  onClose,
}: AddMemberDialogProps) {
  const [user, setUser] = useState<UserCandidate | null>(null);
  const [role, setRole] = useState<OrganizationRole>('member');
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit() {
    if (user === null) {
      return;
    }
    setBusy(true);
    setAlert(null);
    setFieldError(null);
    try {
      onSucceeded(await submit({ userId: user.id, role }));
    } catch (error) {
      const placed = placeMembershipRefusal(error, 'add');
      setFieldError(placed.field);
      setAlert(placed.alert);
      if (placed.reread) {
        onReread();
      }
      setBusy(false);
    }
  }

  return (
    <FormDialog
      open
      title={addMemberDialog.title}
      submitLabel={addMemberDialog.submit}
      busy={busy}
      error={alert}
      submitDisabled={user === null}
      onSubmit={() => void handleSubmit()}
      onClose={onClose}
    >
      <UserSearchField
        label={addMemberDialog.userLabel}
        scope={{ kind: 'organization', organizationId }}
        value={user}
        onChange={(next) => {
          setUser(next);
          setFieldError(null);
        }}
        error={fieldError ?? undefined}
        disabled={busy}
        autoFocus
      />
      <TextField
        select
        label={addMemberDialog.roleLabel}
        value={role}
        disabled={busy}
        helperText={ROLE_DESCRIPTIONS[role]}
        onChange={(event) => setRole(event.target.value as OrganizationRole)}
        slotProps={{ inputLabel: { required: true } }}
      >
        {ORGANIZATION_ROLES.map((option) => (
          <MenuItem key={option} value={option}>
            {ROLE_LABELS[option]}
          </MenuItem>
        ))}
      </TextField>
    </FormDialog>
  );
}
