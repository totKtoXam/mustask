import type { Ref } from 'react';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import { useTheme } from '@mui/material/styles';
import RestoreIcon from '@mui/icons-material/Restore';

/**
 * A deactivated row's «Восстановить» (design § 5.2, § 5.5): it sends at once,
 * with no confirmation; while it runs this row's button shows a spinner and
 * is disabled, and the other rows stay usable. Named «Восстановить
 * «<название>»», which contains its visible label.
 */
export function RestoreButton({
  name,
  busy,
  onClick,
  buttonRef,
}: {
  name: string;
  busy: boolean;
  onClick(): void;
  buttonRef?: Ref<HTMLButtonElement>;
}) {
  const theme = useTheme();
  return (
    <Button
      ref={buttonRef}
      variant="text"
      size="small"
      startIcon={
        busy ? (
          <CircularProgress size={theme.spacing(2)} color="inherit" />
        ) : (
          <RestoreIcon fontSize="small" />
        )
      }
      disabled={busy}
      aria-label={`Восстановить «${name}»`}
      onClick={onClick}
    >
      Восстановить
    </Button>
  );
}
