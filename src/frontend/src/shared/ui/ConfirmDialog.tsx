import { useEffect, useRef, type ReactNode } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import { useTheme } from '@mui/material/styles';
import { DialogTitleBar } from './DialogTitleBar';

export interface ConfirmDialogProps {
  open: boolean;
  title: string;
  /** The dialog's text: what will happen, with the object's name. */
  children: ReactNode;
  /** The primary action; names the action, e.g. «Назначить», «Удалить навсегда». */
  confirmLabel: string;
  /** While the request runs: every control disabled, the dialog not dismissible. */
  busy?: boolean;
  /** A refusal, shown at the top of the body; the dialog stays open. */
  error?: ReactNode;
  /** An irreversible action: the primary takes the error colour (design § 9.4). */
  destructive?: boolean;
  /**
   * The dialog opens with focus on «Отмена», so Enter never confirms by
   * accident (mod-002 design § 4.3). Absent: MUI's initial focus as before.
   */
  cancelAutoFocus?: boolean;
  /**
   * The cancel button's label. Default «Отмена»; the card's unsaved-changes question
   * reads «Продолжить редактирование» (tech-015 design § 5.10).
   */
  cancelLabel?: string;
  onConfirm: () => void;
  onClose: () => void;
}

/**
 * The confirmation dialog (US-9, design § 9.1, § 9.4): a title row with
 * «Закрыть», the body with any refusal above it, and a footer of «Отмена»
 * left of the one filled primary. Escape, «Закрыть» and «Отмена» close an
 * idle dialog and MUI returns focus to the control that opened it; a busy
 * one ignores Escape and a click outside.
 */
export function ConfirmDialog({
  open,
  title,
  children,
  confirmLabel,
  busy = false,
  error,
  destructive = false,
  cancelAutoFocus = false,
  cancelLabel = 'Отмена',
  onConfirm,
  onClose,
}: ConfirmDialogProps) {
  const theme = useTheme();

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="xs" fullWidth>
      <DialogTitleBar title={title} onClose={onClose} busy={busy} />
      <DialogContent sx={{ pt: 0 }}>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}
        {children}
      </DialogContent>
      <DialogActions>
        <CancelButton
          open={open}
          label={cancelLabel}
          focusOnOpen={cancelAutoFocus}
          disabled={busy}
          onClick={onClose}
        />
        <Button
          variant="contained"
          color={destructive ? 'error' : 'primary'}
          onClick={onConfirm}
          disabled={busy}
          startIcon={busy ? <CircularProgress size={theme.spacing(2)} /> : undefined}
        >
          {confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

/**
 * «Отмена», focused each time the dialog opens when `focusOnOpen` is set.
 *
 * Not React's `autoFocus`: that focuses once, at mount, and under
 * `<StrictMode>` (development, `src/main.tsx`) React then runs the new
 * subtree's effects a second time — MUI's focus trap hands the focus back to
 * the opener on the first cleanup and puts it on the dialog's paper on the
 * re-run (mod-002 qa.md D-2). This effect runs on that re-run too, and before
 * the trap's own: it lives inside the dialog, and React runs a child's effects
 * before its ancestor's, so the trap finds the focus already inside and leaves
 * it there. Inside the dialog also because MUI's portal mounts the content one
 * commit after `ConfirmDialog` itself, so an effect up there would see no
 * button yet. `open` in the dependencies covers a dialog reopened while it is
 * still mounted.
 */
function CancelButton({
  open,
  label,
  focusOnOpen,
  disabled,
  onClick,
}: {
  open: boolean;
  label: string;
  focusOnOpen: boolean;
  disabled: boolean;
  onClick: () => void;
}) {
  const ref = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (open && focusOnOpen) {
      ref.current?.focus();
    }
  }, [open, focusOnOpen]);

  return (
    <Button ref={ref} variant="text" onClick={onClick} disabled={disabled}>
      {label}
    </Button>
  );
}
