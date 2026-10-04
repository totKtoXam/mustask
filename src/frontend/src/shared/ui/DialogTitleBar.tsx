import DialogTitle from '@mui/material/DialogTitle';
import IconButton from '@mui/material/IconButton';
import CloseIcon from '@mui/icons-material/Close';

export interface DialogTitleBarProps {
  title: string;
  onClose: () => void;
  /** While a request runs, «Закрыть» is disabled with the rest (US-9). */
  busy?: boolean;
}

/**
 * A dialog's title row (US-9, design § 9.1): the title as the dialog's `h2`
 * and «Закрыть» at its right. The close control is a sibling of the heading,
 * not inside it — inside, the heading's accessible name would read
 * «… Закрыть» (plan § 9, D-5). It sits over the dialog paper's top right
 * corner; the paper is the positioned ancestor.
 */
export function DialogTitleBar({ title, onClose, busy = false }: DialogTitleBarProps) {
  return (
    <>
      <DialogTitle sx={{ pr: 7 }}>{title}</DialogTitle>
      <IconButton
        size="small"
        aria-label="Закрыть"
        onClick={onClose}
        disabled={busy}
        sx={{
          position: 'absolute',
          top: (theme) => theme.spacing(1.5),
          right: (theme) => theme.spacing(2),
        }}
      >
        <CloseIcon />
      </IconButton>
    </>
  );
}
