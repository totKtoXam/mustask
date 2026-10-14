import { useCallback, useMemo, useRef, useState, type ReactNode } from 'react';
import Alert from '@mui/material/Alert';
import Snackbar from '@mui/material/Snackbar';
import { useTheme } from '@mui/material/styles';
import { useNoticeHost } from '../../lib/noticeCorner';
import { NoticeContext, type NoticeApi, type NoticeSeverity } from './noticeContext';

interface Shown {
  /** A serial: a repeat of the same text is a new element, so it is announced again. */
  id: number;
  text: string;
  severity: NoticeSeverity;
}

/** The close button's name on an `Alert` notice: the kit's one «Закрыть». */
const CLOSE_LABEL = 'Закрыть';

/**
 * The one notice host (tech-015 design § 5.12, K-9), mounted once in `AppLayout`: a
 * short result message at the bottom left for `theme.noticeDurationMs`, one at a time —
 * a newer notice replaces the shown one. A click elsewhere, often the next action, does
 * not dismiss it. Never an action or a link in it: that is an `InlineNotice`.
 *
 * It stands above the routes, so a notice survives the change of page that raised it.
 * `plain` is MUI's snackbar message; `success` and `error` are an `Alert` of that
 * severity. Every one is `role="alert"`.
 */
export function NoticeProvider({ children }: { children: ReactNode }) {
  const theme = useTheme();
  const [shown, setShown] = useState<Shown | null>(null);
  const serial = useRef(0);
  const close = useCallback(() => setShown(null), []);
  // One notice at a time in the corner (mod-016 DC-11): the host closes when the
  // shell raises the switcher's fallback or the role-change notice, and showing a
  // notice closes the role-change notice.
  const claim = useNoticeHost(close);

  const show = useCallback(
    (text: string, severity: NoticeSeverity = 'plain') => {
      claim();
      serial.current += 1;
      setShown({ id: serial.current, text, severity });
    },
    [claim],
  );
  const api = useMemo<NoticeApi>(() => ({ show }), [show]);

  return (
    <NoticeContext.Provider value={api}>
      {children}
      {shown && (
        <Snackbar
          key={shown.id}
          open
          autoHideDuration={theme.noticeDurationMs}
          onClose={(_event, reason) => {
            if (reason !== 'clickaway') close();
          }}
          anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
          message={shown.severity === 'plain' ? shown.text : undefined}
        >
          {shown.severity === 'plain' ? undefined : (
            <Alert severity={shown.severity} onClose={close} closeText={CLOSE_LABEL}>
              {shown.text}
            </Alert>
          )}
        </Snackbar>
      )}
    </NoticeContext.Provider>
  );
}
