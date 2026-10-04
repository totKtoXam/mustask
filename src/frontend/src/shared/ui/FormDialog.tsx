import type { ReactNode } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import Stack from '@mui/material/Stack';
import { useTheme } from '@mui/material/styles';
import { DialogTitleBar } from './DialogTitleBar';

export interface FormDialogProps {
  open: boolean;
  title: string;
  /** The fields, in one column. */
  children: ReactNode;
  /** The primary action, e.g. «Создать». */
  submitLabel: string;
  busy?: boolean;
  /** An error not tied to a field, above the first field. */
  error?: ReactNode;
  /** Called on submit; the browser's own submission is prevented. */
  onSubmit: () => void;
  onClose: () => void;
}

/**
 * Create in a dialog (US-9, design § 9.5): the confirmation's anatomy around
 * a form. The dialog paper is the `form`, so Enter in a field submits; the
 * footer is «Отмена» left of the filled submit. Busy: every control
 * disabled, Escape and a click outside ignored.
 */
export function FormDialog({
  open,
  title,
  children,
  submitLabel,
  busy = false,
  error,
  onSubmit,
  onClose,
}: FormDialogProps) {
  const theme = useTheme();

  return (
    <Dialog
      open={open}
      onClose={busy ? undefined : onClose}
      maxWidth="sm"
      fullWidth
      slotProps={{
        paper: {
          component: 'form',
          onSubmit: (event: { preventDefault(): void }) => {
            event.preventDefault();
            onSubmit();
          },
        },
      }}
    >
      <DialogTitleBar title={title} onClose={onClose} busy={busy} />
      <DialogContent sx={{ pt: 0 }}>
        <Stack spacing={2} sx={{ pt: 1 }}>
          {error && <Alert severity="error">{error}</Alert>}
          {children}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onClose} disabled={busy}>
          Отмена
        </Button>
        <Button
          type="submit"
          variant="contained"
          disabled={busy}
          startIcon={busy ? <CircularProgress size={theme.spacing(2)} /> : undefined}
        >
          {submitLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
