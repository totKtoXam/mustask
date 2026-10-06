import { useCallback, useState, type ReactNode } from 'react';
import Alert from '@mui/material/Alert';
import Snackbar from '@mui/material/Snackbar';

/** How long a notice stays (spec § Notices; mod-015 § 8.2). */
const NOTICE_DURATION_MS = 6000;

interface Shown {
  id: number;
  text: string;
  severity: 'success' | 'error';
}

/**
 * A page's one notice, 6 s at the bottom left; a newer one replaces it:
 * «Рабочий элемент <ID> создан.» as plain text (mod-003's), a refused move as
 * an `Alert severity="error"` (spec § Notices).
 */
export function useNotice(): {
  notice: ReactNode;
  showSuccess(text: string): void;
  showError(text: string): void;
} {
  const [shown, setShown] = useState<Shown | null>(null);
  const show = useCallback((text: string, severity: Shown['severity']) => {
    setShown((current) => ({ id: (current?.id ?? 0) + 1, text, severity }));
  }, []);
  const showSuccess = useCallback((text: string) => show(text, 'success'), [show]);
  const showError = useCallback((text: string) => show(text, 'error'), [show]);
  const close = () => setShown(null);

  const notice = shown && (
    <Snackbar
      key={shown.id}
      open
      autoHideDuration={NOTICE_DURATION_MS}
      // A click elsewhere — often the next move — does not dismiss it.
      onClose={(_event, reason) => {
        if (reason !== 'clickaway') {
          close();
        }
      }}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
      message={shown.severity === 'success' ? shown.text : undefined}
    >
      {shown.severity === 'error' ? (
        <Alert severity="error" onClose={close}>
          {shown.text}
        </Alert>
      ) : undefined}
    </Snackbar>
  );
  return { notice, showSuccess, showError };
}
