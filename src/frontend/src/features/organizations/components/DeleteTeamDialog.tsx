import { useState } from 'react';
import Typography from '@mui/material/Typography';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { ConfirmDialog, FieldsSkeleton, InlineNotice } from '../../../shared/ui';
import { useOwnedItemsCount } from '../hooks/queries';
import { placeRefusal } from '../lib/refusals';

/**
 * «Удаление команды» (mod-017 US-7, BR-OT-12; design § 7.2): destructive, with no
 * restore, focus on «Отмена». The count of the items the team owns, in any state,
 * is read when the dialog opens: while it is read a skeleton stands for the text
 * and «Удалить команду» is unavailable; a failed read says so with «Повторить» and
 * deletion stays unavailable. An item given to the team while the dialog is open
 * is not in the count and is reassigned all the same. A refusal (the team is gone,
 * or it is the project's last) shows in the dialog's alert, and the page reads
 * itself again when the dialog closes.
 */
export function DeleteTeamDialog<R>({
  organizationId,
  projectId,
  teamId,
  teamName,
  action,
  onSucceeded,
  onClose,
}: {
  organizationId: string;
  projectId: string;
  teamId: string;
  teamName: string;
  action(): Promise<R>;
  onSucceeded(result: R): void;
  /** `contextGone`: a refusal that leaves the page stale; the page reads itself again. */
  onClose(contextGone: boolean): void;
}) {
  const count = useOwnedItemsCount(organizationId, projectId, teamId);
  const [busy, setBusy] = useState(false);
  const [alert, setAlert] = useState<string | null>(null);
  const [stale, setStale] = useState(false);

  async function confirm() {
    setBusy(true);
    setAlert(null);
    try {
      onSucceeded(await action());
    } catch (error) {
      const placed = placeRefusal(error, []);
      setAlert(placed.alert);
      // The team is gone or the last one: whatever the page shows is no longer true.
      setStale(true);
      setBusy(false);
    }
  }

  const counting = count.isPending || (count.isError && count.isFetching);
  const owned = count.data?.count ?? 0;
  const text =
    owned > 0
      ? `Удалить команду «${teamName}»? Её рабочие элементы (${owned}) станут «Без команды». Восстановить команду будет нельзя.`
      : `Удалить команду «${teamName}»? Восстановить команду будет нельзя.`;

  return (
    <ConfirmDialog
      open
      title="Удаление команды"
      confirmLabel="Удалить команду"
      destructive
      cancelAutoFocus
      busy={busy}
      error={alert}
      confirmDisabled={!count.isSuccess}
      onConfirm={() => void confirm()}
      onClose={() => onClose(stale)}
    >
      {counting ? (
        <FieldsSkeleton count={1} />
      ) : count.isError ? (
        <InlineNotice
          severity="error"
          action={{ label: 'Повторить', onClick: () => void count.refetch() }}
        >
          {apiErrorText(count.error)}
        </InlineNotice>
      ) : (
        <Typography variant="body1">{text}</Typography>
      )}
    </ConfirmDialog>
  );
}
