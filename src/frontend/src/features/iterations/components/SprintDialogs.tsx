import { useNotice } from '../../../shared/ui';
import type { SprintRow } from '../api/iterationsApi';
import type { SprintRefusal } from '../lib/refusals';
import { sprintTexts } from '../lib/sprintTexts';
import { CloseSprintDialog } from './CloseSprintDialog';
import { RemoveSprintDialog, StartSprintDialog } from './SprintConfirmDialogs';
import { SprintFormDialog } from './SprintFormDialog';

/** Which dialog is open over the list or the outcome page, if any. */
export type SprintDialog =
  | null
  | { kind: 'create' }
  | { kind: 'edit' | 'rename' | 'start' | 'remove' | 'close'; row: SprintRow };

/**
 * The five dialogs of design § 5, mounted per opening. A write's success shows its notice and has
 * the host read again; a refusal that leaves the page stale (the code lost, the team or the sprint
 * gone) closes the dialog, says «Недостаточно прав» for a lost code and has the host read again.
 */
export function SprintDialogs({
  organizationId,
  teamId,
  dialog,
  onChange,
  onRefresh,
  onWritten,
}: {
  organizationId: string;
  teamId: string;
  dialog: SprintDialog;
  onChange(next: SprintDialog): void;
  /** Re-read the page: after a refusal, or a success. */
  onRefresh(options: { codes: boolean }): void;
  /** A write was accepted (the notice is shown here). */
  onWritten?(): void;
}) {
  const notice = useNotice();
  if (!dialog) return null;

  const done = (text: string) => {
    onChange(null);
    notice.show(text);
    onRefresh({ codes: false });
    onWritten?.();
  };
  const closed = (refusal: SprintRefusal | null) => {
    onChange(null);
    if (refusal) {
      if (refusal.forbidden) notice.show(sprintTexts.list.forbidden, 'error');
      onRefresh({ codes: refusal.forbidden });
    }
  };
  const common = { organizationId, teamId, onDone: done, onClose: closed };

  switch (dialog.kind) {
    case 'create':
      return <SprintFormDialog {...common} mode="create" />;
    case 'edit':
    case 'rename':
      return <SprintFormDialog {...common} mode={dialog.kind} sprint={dialog.row} />;
    case 'start':
      return (
        <StartSprintDialog
          {...common}
          sprint={dialog.row}
          onSetDates={() => onChange({ kind: 'edit', row: dialog.row })}
          onStale={() => onRefresh({ codes: false })}
        />
      );
    case 'remove':
      return <RemoveSprintDialog {...common} sprint={dialog.row} />;
    case 'close':
      return <CloseSprintDialog {...common} sprint={dialog.row} />;
  }
}
