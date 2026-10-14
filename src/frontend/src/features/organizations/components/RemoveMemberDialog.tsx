import { useState } from 'react';
import Typography from '@mui/material/Typography';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import type { Member } from '../api/membershipApi';
import { removeMemberDialog } from '../lib/memberTexts';
import { placeMembershipRefusal } from '../lib/membershipRefusals';
import { plainName } from '../lib/userName';

export interface RemoveMemberDialogProps<R> {
  member: Member;
  organizationName: string;
  /** The row is the caller's own: the other title and the other text. */
  isSelf: boolean;
  action(): Promise<R>;
  onSucceeded(result: R): void;
  /** «Участник не найден.»: the list is out of date, read it again at once. */
  onReread(): void;
  onClose(): void;
}

/**
 * «Удалить участника?» and «Удалить себя из организации?» (US-1, design
 * § 3.7): the mod-015 confirmation opened with focus on «Отмена», with the
 * destructive primary — a removal also ends the user's team memberships,
 * which adding them again does not bring back (DC-5). Someone else reads
 * «Пользователь <имя> потеряет доступ…» (an unknown user without «Пользователь»,
 * the name without the mark); oneself «Вы потеряете доступ…». «В организации
 * должен остаться хотя бы один администратор» and «Участник не найден.» are
 * the dialog's `Alert`. Mounted per opening.
 */
export function RemoveMemberDialog<R>({
  member,
  organizationName,
  isSelf,
  action,
  onSucceeded,
  onReread,
  onClose,
}: RemoveMemberDialogProps<R>) {
  const [busy, setBusy] = useState(false);
  const [alert, setAlert] = useState<string | null>(null);

  async function confirm() {
    setBusy(true);
    setAlert(null);
    try {
      onSucceeded(await action());
    } catch (error) {
      const placed = placeMembershipRefusal(error, 'remove');
      setAlert(placed.alert);
      if (placed.reread) {
        onReread();
      }
      setBusy(false);
    }
  }

  const name = member.user.state === 'unknown' ? null : plainName(member.user);
  return (
    <ConfirmDialog
      open
      title={isSelf ? removeMemberDialog.selfTitle : removeMemberDialog.otherTitle}
      confirmLabel={removeMemberDialog.submit}
      destructive
      cancelAutoFocus
      busy={busy}
      error={alert}
      onConfirm={() => void confirm()}
      onClose={onClose}
    >
      <Typography variant="body1">
        {isSelf
          ? removeMemberDialog.selfText(organizationName)
          : removeMemberDialog.otherText(name, organizationName)}
      </Typography>
    </ConfirmDialog>
  );
}
