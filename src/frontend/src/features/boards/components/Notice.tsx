import { useCallback, useState, type ReactNode } from 'react';
import Alert from '@mui/material/Alert';
import Snackbar from '@mui/material/Snackbar';
import { useNoticeHost } from '../../../shared/lib/noticeCorner';

/** How long a notice stays (spec § Notices; mod-015 § 8.2). */
const NOTICE_DURATION_MS = 6000;

interface Shown {
  id: number;
  text: string;
  /** `plain`: mod-003's creation notice, a plain message. */
  severity: 'plain' | 'success' | 'error';
}

/**
 * A page's one notice, 6 s at the bottom left; a newer one replaces it:
 * «Рабочий элемент <ID> создан.» as plain text (mod-003's), a parent written
 * from the backlog as an `Alert severity="success"` (feat-001 spec § Notices
 * on «Бэклоги»), a refused move or parent write as an `Alert
 * severity="error"` (spec § Notices). One notice at a time in the corner
 * (mod-016 DC-11): it closes when the shell raises the switcher's fallback or
 * the role-change notice, and showing one closes the role-change notice
 * (`useNoticeHost`).
 */
export function useNotice(): {
  notice: ReactNode;
  showSuccess(text: string): void;
  showSuccessAlert(text: string): void;
  showError(text: string): void;
} {
  const [shown, setShown] = useState<Shown | null>(null);
  const close = () => setShown(null);
  const claim = useNoticeHost(close);
  const show = useCallback(
    (text: string, severity: Shown['severity']) => {
      claim();
      setShown((current) => ({ id: (current?.id ?? 0) + 1, text, severity }));
    },
    [claim],
  );
  const showSuccess = useCallback((text: string) => show(text, 'plain'), [show]);
  const showSuccessAlert = useCallback((text: string) => show(text, 'success'), [show]);
  const showError = useCallback((text: string) => show(text, 'error'), [show]);

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
      message={shown.severity === 'plain' ? shown.text : undefined}
    >
      {shown.severity === 'plain' ? undefined : (
        <Alert severity={shown.severity} onClose={close}>
          {shown.text}
        </Alert>
      )}
    </Snackbar>
  );
  return { notice, showSuccess, showSuccessAlert, showError };
}
