import { useState } from 'react';
import { FormDialog } from '../../../shared/ui/FormDialog';
import type { UserCandidate, UserRef } from '../api/membershipApi';
import { addTeamMemberDialog } from '../lib/memberTexts';
import { placeMembershipRefusal } from '../lib/membershipRefusals';
import { UserSearchField } from './UserSearchField';

export interface AddTeamMemberDialogProps {
  organizationId: string;
  projectId: string;
  teamId: string;
  /** Sends the add; resolves with the user as the team now lists them. */
  submit(userId: string): Promise<UserRef>;
  onSucceeded(user: UserRef): void;
  /** «Пользователь уже состоит в команде»: the table is out of date, read it in the background. */
  onReread(): void;
  /** A project or team of the address is gone: the page reads itself again and shows its frame. */
  onContextGone(): void;
  onClose(): void;
}

/**
 * «Добавление в команду» (US-3, design § 4.4): «Участник организации» — a
 * search among the active members of the organization, focused, with the
 * helper «Поиск среди активных участников организации» until a refusal
 * replaces it. «Добавить» is disabled until a participant is chosen. Users
 * already in the team are offered; the server's refusals land on the field with
 * the chosen user kept, so another can be picked, any other refusal in the
 * dialog's `Alert`. Mounted per opening.
 */
export function AddTeamMemberDialog({
  organizationId,
  projectId,
  teamId,
  submit,
  onSucceeded,
  onReread,
  onContextGone,
  onClose,
}: AddTeamMemberDialogProps) {
  const [user, setUser] = useState<UserCandidate | null>(null);
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
      onSucceeded(await submit(user.id));
    } catch (error) {
      const placed = placeMembershipRefusal(error, 'teamAdd');
      setFieldError(placed.field);
      setAlert(placed.alert);
      if (placed.reread) {
        onReread();
      }
      if (placed.contextGone) {
        onContextGone();
      }
      setBusy(false);
    }
  }

  return (
    <FormDialog
      open
      title={addTeamMemberDialog.title}
      submitLabel={addTeamMemberDialog.submit}
      busy={busy}
      error={alert}
      submitDisabled={user === null}
      onSubmit={() => void handleSubmit()}
      onClose={onClose}
    >
      <UserSearchField
        label={addTeamMemberDialog.userLabel}
        scope={{ kind: 'team', organizationId, projectId, teamId }}
        value={user}
        onChange={(next) => {
          setUser(next);
          setFieldError(null);
        }}
        error={fieldError ?? undefined}
        helperText={addTeamMemberDialog.helper}
        disabled={busy}
        autoFocus
      />
    </FormDialog>
  );
}
