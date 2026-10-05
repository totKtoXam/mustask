import { useCallback, useState } from 'react';
import { Outlet } from 'react-router-dom';
import Alert from '@mui/material/Alert';
import Snackbar from '@mui/material/Snackbar';
import { useWorkContext } from '../../../shared/lib/workContext';
import { NotifyContext, type Notify, type OrganizationsNotice } from './notice';

/** How long a notice stays (design § 4.5). */
const NOTICE_DURATION_MS = 6000;

interface ShownNotice extends OrganizationsNotice {
  id: number;
}

/**
 * The pathless layout route around the slice's nine routes (plan-frontend
 * § 7.4): it hosts the slice's one `Snackbar`, 6 s at the bottom left, so a
 * notice survives the page an action opens. One notice at a time (DC-7): the
 * switcher's fallback notice wins, so this one closes when the switcher
 * raises a fallback.
 */
export function OrganizationsSection() {
  const [notice, setNotice] = useState<ShownNotice | null>(null);

  const notify = useCallback<Notify>((text, severity = 'success') => {
    setNotice((current) => ({ id: (current?.id ?? 0) + 1, text, severity }));
  }, []);

  const fallbackId = useWorkContext().fallback?.id ?? null;
  const [seenFallbackId, setSeenFallbackId] = useState(fallbackId);
  if (seenFallbackId !== fallbackId) {
    setSeenFallbackId(fallbackId);
    if (fallbackId !== null) {
      setNotice(null);
    }
  }

  const close = () => setNotice(null);

  return (
    <NotifyContext.Provider value={notify}>
      <Outlet />
      {notice && (
        <Snackbar
          key={notice.id}
          open
          autoHideDuration={NOTICE_DURATION_MS}
          // It stays its 6 s: a click elsewhere — often the action that
          // raises the next notice — does not dismiss it.
          onClose={(_event, reason) => {
            if (reason !== 'clickaway') {
              close();
            }
          }}
          anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
          message={notice.severity === 'success' ? notice.text : undefined}
        >
          {notice.severity === 'error' ? (
            <Alert severity="error" onClose={close}>
              {notice.text}
            </Alert>
          ) : undefined}
        </Snackbar>
      )}
    </NotifyContext.Provider>
  );
}
