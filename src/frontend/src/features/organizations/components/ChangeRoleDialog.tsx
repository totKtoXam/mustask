import { useState } from 'react';
import Alert from '@mui/material/Alert';
import MenuItem from '@mui/material/MenuItem';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { FormDialog } from '../../../shared/ui/FormDialog';
import type { Member } from '../api/membershipApi';
import { changeRoleDialog } from '../lib/memberTexts';
import { placeMembershipRefusal } from '../lib/membershipRefusals';
import {
  ORGANIZATION_ROLES,
  ROLE_DESCRIPTIONS,
  ROLE_LABELS,
  type OrganizationRole,
} from '../lib/roles';
import { UserLine } from './UserLine';

export interface ChangeRoleDialogProps {
  member: Member;
  /** The row is the caller's own: stepping down from administrator warns first. */
  isSelf: boolean;
  /** Sends the change; resolves with the membership as it now stands. */
  submit(role: OrganizationRole): Promise<Member>;
  onSucceeded(member: Member): void;
  /** «Участник не найден.»: the list is out of date, read it again at once. */
  onReread(): void;
  onClose(): void;
}

/**
 * «Изменение роли» (US-1, design § 3.6): who is being changed, «Роль»
 * preselected with the current one and focused, its helper saying what the
 * chosen role may do, and — only when the caller edits their own row, which
 * reads «Администратор», and picks another role — the warning that only
 * another administrator can give the role back. «Сохранить» is disabled until
 * the role differs. «В организации должен остаться хотя бы один
 * администратор» is the helper of «Роль»; any other refusal is the dialog's
 * `Alert`. Mounted per opening.
 */
export function ChangeRoleDialog({
  member,
  isSelf,
  submit,
  onSucceeded,
  onReread,
  onClose,
}: ChangeRoleDialogProps) {
  const [role, setRole] = useState<OrganizationRole>(member.role);
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const steppingDown = isSelf && member.role === 'admin' && role !== 'admin';

  async function handleSubmit() {
    setBusy(true);
    setAlert(null);
    setFieldError(null);
    try {
      onSucceeded(await submit(role));
    } catch (error) {
      const placed = placeMembershipRefusal(error, 'role');
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
      title={changeRoleDialog.title}
      submitLabel={changeRoleDialog.submit}
      busy={busy}
      error={alert}
      submitDisabled={role === member.role}
      onSubmit={() => void handleSubmit()}
      onClose={onClose}
    >
      <Typography variant="body2" color="text.secondary">
        {'Пользователь: '}
        <UserLine user={member.user} truncate={false} />
      </Typography>
      <TextField
        select
        autoFocus
        label={changeRoleDialog.roleLabel}
        value={role}
        disabled={busy}
        error={fieldError !== null}
        helperText={fieldError ?? ROLE_DESCRIPTIONS[role]}
        onChange={(event) => {
          setRole(event.target.value as OrganizationRole);
          setFieldError(null);
        }}
        slotProps={{ inputLabel: { required: true } }}
      >
        {ORGANIZATION_ROLES.map((option) => (
          <MenuItem key={option} value={option}>
            {ROLE_LABELS[option]}
          </MenuItem>
        ))}
      </TextField>
      {steppingDown && <Alert severity="warning">{changeRoleDialog.selfDemotion}</Alert>}
    </FormDialog>
  );
}
