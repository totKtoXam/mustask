import { useRef, useState } from 'react';
import Typography from '@mui/material/Typography';
import { ConfirmDialog } from '../../../../shared/ui';
import type { WorkItem } from '../../api/workItemsApi';
import { useDeleteWorkItem } from '../../hooks/mutations';
import { deletionTexts } from '../../lib/cardTexts';
import { deletionRefusal, type DeletionRefusal } from '../../lib/deletionRefusals';

/**
 * «Удалить рабочий элемент?» (mod-005 US-1; design § 3): a confirmation over the card that names
 * the identifier and what happens, in the primary colour — the action is reversible (mod-015
 * § 9.4). It sends one Delete (`busy` while the request runs; a second activation sends
 * nothing) and hands what happened to the card: the item as stored, a refusal that leaves the
 * card stale (`…deleted`: another user got there first) or gone, or — for a failure that
 * changes nothing — an error above the body, the dialog open and the primary trying again.
 */
export function DeleteWorkItemDialog({
  organizationId,
  projectId,
  identifier,
  liveChildren,
  hasDraft,
  onDeleted,
  onRefused,
  onClose,
}: {
  organizationId: string;
  projectId: string;
  identifier: string;
  /** The card's «Дочерние элементы (n)»: the live children, the read omits the deleted. */
  liveChildren: number;
  /** The card holds unsaved edits: they are lost, and the text says so. */
  hasDraft: boolean;
  /** The item as stored: «Удалена», with its `deletion`. */
  onDeleted: (item: WorkItem) => void;
  /** `gone` or `stale`: the dialog closes and the card does the rest. */
  onRefused: (refusal: Exclude<DeletionRefusal, { kind: 'stays' }>) => void;
  onClose: () => void;
}) {
  const mutation = useDeleteWorkItem(organizationId, projectId, identifier);
  const [error, setError] = useState<string | null>(null);
  const sending = useRef(false);
  // The sentence is what the user was asked: the card turns deleted under the dialog the moment
  // the answer is cached, with its draft dropped, and the dialog must not change its words then.
  const [text] = useState(() => deletionTexts.delete.text(identifier, liveChildren, hasDraft));

  async function confirm() {
    if (sending.current) {
      return;
    }
    sending.current = true;
    setError(null);
    try {
      onDeleted(await mutation.mutateAsync());
    } catch (caught) {
      const refusal = deletionRefusal(caught);
      if (refusal.kind === 'stays') {
        setError(refusal.text);
      } else {
        onRefused(refusal);
      }
    } finally {
      sending.current = false;
    }
  }

  const texts = deletionTexts.delete;
  return (
    <ConfirmDialog
      open
      title={texts.title}
      confirmLabel={texts.confirm}
      busy={mutation.isPending}
      error={error}
      onConfirm={() => void confirm()}
      onClose={onClose}
    >
      <Typography variant="body1">{text}</Typography>
    </ConfirmDialog>
  );
}
