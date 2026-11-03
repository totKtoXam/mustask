import { useRef, useState } from 'react';
import Typography from '@mui/material/Typography';
import { InlineNotice, InlineTextArea } from '../../../shared/ui';
import { FormDialog } from '../../../shared/ui/FormDialog';
import type { UserCandidate } from '../api/membershipApi';
import type { Appointment } from '../api/rescueApi';
import { reasonProblem, trimmedReason } from '../lib/appointmentReason';
import { placeAppointmentRefusal } from '../lib/rescueRefusals';
import { appointDialog } from '../lib/rescueTexts';
import { UserSearchField } from './UserSearchField';

export interface AppointAdminDialogProps {
  /** The row's name, shown whole (design § 4.1). */
  organizationName: string;
  /** Sends the appointment; resolves with the record just written. */
  submit(body: { userId: string; reason: string }): Promise<Appointment>;
  onSucceeded(appointment: Appointment): void;
  /** 403: the page turns into «Доступ ограничен» (DC-8). */
  onForbidden(): void;
  /** The organization is gone: the list is read again in the background (design § 4.2). */
  onReread(): void;
  onClose(): void;
}

/**
 * «Назначение администратора» (US-2, design § 4): the lead, the Keycloak warning from the first
 * frame, «Пользователь» (a search with no organization, decision D-4) and a required «Причина».
 * «Назначить» is disabled until a user is chosen and enabled with an empty reason: the reason is
 * checked on the press, in code points of the trimmed text (DC-6), and nothing is sent while it
 * is invalid. The server's refusals land on the field they are about; the chosen values stay.
 * Mounted per opening.
 */
export function AppointAdminDialog({
  organizationName,
  submit,
  onSucceeded,
  onForbidden,
  onReread,
  onClose,
}: AppointAdminDialogProps) {
  const [user, setUser] = useState<UserCandidate | null>(null);
  const [reason, setReason] = useState('');
  const [userError, setUserError] = useState<string | null>(null);
  const [reasonError, setReasonError] = useState<string | null>(null);
  const [alert, setAlert] = useState<string | null>(null);
  const [gone, setGone] = useState(false);
  const [busy, setBusy] = useState(false);
  const reasonRef = useRef<HTMLTextAreaElement>(null);

  async function handleSubmit() {
    if (user === null || gone) {
      return;
    }
    const problem = reasonProblem(reason);
    if (problem !== null) {
      setReasonError(
        problem === 'required' ? appointDialog.reasonRequired : appointDialog.reasonLimit,
      );
      reasonRef.current?.focus();
      return;
    }
    setBusy(true);
    setAlert(null);
    setUserError(null);
    setReasonError(null);
    try {
      onSucceeded(await submit({ userId: user.id, reason: trimmedReason(reason) }));
    } catch (error) {
      const placed = placeAppointmentRefusal(error);
      if (placed.forbidden) {
        onForbidden();
        return;
      }
      setUserError(placed.userError);
      setReasonError(placed.reasonError);
      setAlert(placed.alert);
      if (placed.organizationGone) {
        setGone(true);
        onReread();
      }
      setBusy(false);
    }
  }

  return (
    <FormDialog
      open
      title={appointDialog.title}
      submitLabel={appointDialog.submit}
      busy={busy}
      submitDisabled={user === null || gone}
      onSubmit={() => void handleSubmit()}
      onClose={onClose}
    >
      <Typography variant="body2">{appointDialog.lead(organizationName)}</Typography>
      <InlineNotice severity="warning">{appointDialog.keycloakWarning}</InlineNotice>
      {gone && <InlineNotice severity="error">{appointDialog.organizationMissing}</InlineNotice>}
      {alert && <InlineNotice severity="error">{alert}</InlineNotice>}
      <UserSearchField
        label={appointDialog.userLabel}
        scope={{ kind: 'appointee' }}
        value={user}
        onChange={(next) => {
          setUser(next);
          setUserError(null);
        }}
        error={userError ?? undefined}
        disabled={busy}
        autoFocus
      />
      <InlineTextArea
        label={appointDialog.reasonLabel}
        frame="always"
        required
        minRows={3}
        value={reason}
        onChange={(next) => {
          setReason(next);
          setReasonError(null);
        }}
        hint={appointDialog.reasonLimit}
        error={reasonError ?? undefined}
        busy={busy}
        ref={reasonRef}
      />
    </FormDialog>
  );
}
