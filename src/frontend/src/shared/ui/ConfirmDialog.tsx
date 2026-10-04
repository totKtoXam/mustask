import type { ReactNode } from 'react';
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
        <Button variant="text" onClick={onClose} disabled={busy}>
          Отмена
        </Button>
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
