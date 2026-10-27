import { useRef, useState } from 'react';
import Typography from '@mui/material/Typography';
import { ConfirmDialog } from '../../../../shared/ui';
import { usePurgeWorkItem } from '../../hooks/mutations';
import { deletionTexts } from '../../lib/cardTexts';
import { deletionRefusal, type DeletionRefusal } from '../../lib/deletionRefusals';

/**
 * «Удалить рабочий элемент навсегда?» (mod-005 US-6; design § 6): the administrator's
 * confirmation that the item goes for good. Destructive, with focus on «Отмена» so Enter never
 * confirms by accident, and no typed confirmation (OQ-4 A). The text names only what exists —
 * the identifier, the title and the live children that lose their parent; each later line that
 * gives an item data extends the sentence (BR-DR-16). It sends one Purge; a refusal that leaves
 * the card stale or gone is the card's to place (design § 8), and a failure that changes nothing
 * stays above the body with the dialog open.
 */
export function PurgeWorkItemDialog({
  organizationId,
  projectId,
  identifier,
  title,
  liveChildren,
  onPurged,
  onRefused,
  onClose,
}: {
  organizationId: string;
  projectId: string;
  identifier: string;
  /** The stored title; it wraps wherever it must. */
  title: string;
  /** The card's «Дочерние элементы (n)»: the live children, which are left without a parent. */
  liveChildren: number;
  onPurged: () => void;
  /** `gone` or `stale`: the dialog closes and the card does the rest. */
  onRefused: (refusal: Exclude<DeletionRefusal, { kind: 'stays' }>) => void;
  onClose: () => void;
}) {
  const mutation = usePurgeWorkItem(organizationId, projectId, identifier);
  const [error, setError] = useState<string | null>(null);
  const sending = useRef(false);
  // The sentence is what the user was asked, whatever the cache does under the dialog.
  const [text] = useState(() => deletionTexts.purgeDialog.text(identifier, title, liveChildren));

  async function confirm() {
    if (sending.current) {
      return;
    }
    sending.current = true;
    setError(null);
    try {
      await mutation.mutateAsync();
      onPurged();
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

  const texts = deletionTexts.purgeDialog;
  return (
    <ConfirmDialog
      open
      title={texts.title}
      confirmLabel={texts.confirm}
      destructive
      cancelAutoFocus
      busy={mutation.isPending}
      error={error}
      onConfirm={() => void confirm()}
      onClose={onClose}
    >
      <Typography variant="body1" sx={{ overflowWrap: 'anywhere' }}>
        {text}
      </Typography>
    </ConfirmDialog>
  );
}
