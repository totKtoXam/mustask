import { useState } from 'react';
import Typography from '@mui/material/Typography';
import { ConfirmDialog, InlineNotice } from '../../../shared/ui';
import { iterationErrorCodes } from '../../../shared/api/errorCodes';
import type { SprintRow } from '../api/iterationsApi';
import { useRemoveSprint, useStartSprint } from '../hooks/mutations';
import { placeSprintRefusal, type SprintRefusal } from '../lib/refusals';
import { sprintTexts } from '../lib/sprintTexts';

interface ConfirmProps {
  organizationId: string;
  teamId: string;
  sprint: SprintRow;
  /** The write was accepted: the host shows the notice and reads the list again. */
  onDone(text: string): void;
  /** `refusal`: what the server said when it left the page stale; the host reads the list again. */
  onClose(refusal: SprintRefusal | null): void;
}

/** Whether a refusal closes the dialog and leaves the page stale (design § 5.3, § 5.4). */
function closesDialog(refusal: SprintRefusal): boolean {
  return (
    refusal.teamGone ||
    refusal.forbidden ||
    refusal.sprintGone ||
    refusal.code === iterationErrorCodes.notPlanned
  );
}

/**
 * «Начать спринт» (design § 5.3). A sprint with no dates is explained, and the primary becomes
 * «Указать даты» (`onSetDates`). «У команды уже есть активный спринт.» stays in the dialog and the
 * list is read again; the other refusals close it.
 */
export function StartSprintDialog({
  organizationId,
  teamId,
  sprint,
  onDone,
  onClose,
  onSetDates,
  onStale,
}: ConfirmProps & { onSetDates(): void; onStale(): void }) {
  const start = useStartSprint(organizationId, teamId);
  const [busy, setBusy] = useState(false);
  const [alert, setAlert] = useState<string | null>(null);
  const undated = sprint.startDate === null || sprint.endDate === null;

  async function confirm() {
    if (undated) {
      onSetDates();
      return;
    }
    setBusy(true);
    setAlert(null);
    try {
      await start.mutateAsync(sprint.id);
      onDone(sprintTexts.notices.started);
    } catch (error) {
      const refusal = placeSprintRefusal(error);
      if (closesDialog(refusal)) {
        onClose(refusal);
        return;
      }
      setAlert(refusal.text);
      onStale();
      setBusy(false);
    }
  }

  return (
    <ConfirmDialog
      open
      title={sprintTexts.start.title}
      confirmLabel={undated ? sprintTexts.start.setDates : sprintTexts.start.confirm}
      busy={busy}
      error={alert}
      onConfirm={() => void confirm()}
      onClose={() => onClose(null)}
    >
      {undated && <InlineNotice severity="warning">{sprintTexts.start.noDates}</InlineNotice>}
      <Typography variant="body1">{sprintTexts.start.text(sprint.name)}</Typography>
    </ConfirmDialog>
  );
}

/**
 * «Удалить спринт» (design § 5.4): destructive. The refusal names no item, project or count; a
 * missing sprint closes the dialog.
 */
export function RemoveSprintDialog({
  organizationId,
  teamId,
  sprint,
  onDone,
  onClose,
}: ConfirmProps) {
  const remove = useRemoveSprint(organizationId, teamId);
  const [busy, setBusy] = useState(false);
  const [alert, setAlert] = useState<string | null>(null);

  async function confirm() {
    setBusy(true);
    setAlert(null);
    try {
      await remove.mutateAsync(sprint.id);
      onDone(sprintTexts.notices.removed);
    } catch (error) {
      const refusal = placeSprintRefusal(error);
      if (closesDialog(refusal)) {
        onClose(refusal);
        return;
      }
      setAlert(refusal.text);
      setBusy(false);
    }
  }

  return (
    <ConfirmDialog
      open
      title={sprintTexts.remove.title}
      confirmLabel={sprintTexts.remove.confirm}
      destructive
      cancelAutoFocus
      busy={busy}
      error={alert}
      onConfirm={() => void confirm()}
      onClose={() => onClose(null)}
    >
      <Typography variant="body1">{sprintTexts.remove.text(sprint.name)}</Typography>
    </ConfirmDialog>
  );
}
