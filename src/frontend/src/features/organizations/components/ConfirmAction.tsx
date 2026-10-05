import { useState, type ReactNode } from 'react';
import Typography from '@mui/material/Typography';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
import { placeRefusal } from '../lib/refusals';

export interface ConfirmActionProps<R> {
  title: string;
  /** What will happen, with the object's name (the design's text, verbatim). */
  text: ReactNode;
  confirmLabel: string;
  /** Cannot be undone: the error-coloured primary (design § 4.3). */
  destructive?: boolean;
  action(): Promise<R>;
  onSucceeded(result: R): void;
  /** `contextGone`: a not-found answer; the page reads itself again (design § 4.4). */
  onClose(contextGone: boolean): void;
}

/**
 * A confirmation of this slice (design § 4.3): mod-015's `ConfirmDialog`,
 * opened with focus on «Отмена» so Enter never confirms by accident; busy
 * while the request runs; a refusal in its `Alert`, the dialog kept open.
 * Mounted per opening.
 */
export function ConfirmAction<R>({
  title,
  text,
  confirmLabel,
  destructive = false,
  action,
  onSucceeded,
  onClose,
}: ConfirmActionProps<R>) {
  const [busy, setBusy] = useState(false);
  const [alert, setAlert] = useState<string | null>(null);
  const [contextGone, setContextGone] = useState(false);

  async function confirm() {
    setBusy(true);
    setAlert(null);
    try {
      onSucceeded(await action());
    } catch (error) {
      const placed = placeRefusal(error, []);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
    }
  }

  return (
    <ConfirmDialog
      open
      title={title}
      confirmLabel={confirmLabel}
      destructive={destructive}
      cancelAutoFocus
      busy={busy}
      error={alert}
      onConfirm={() => void confirm()}
      onClose={() => onClose(contextGone)}
    >
      <Typography variant="body1">{text}</Typography>
    </ConfirmDialog>
  );
}
