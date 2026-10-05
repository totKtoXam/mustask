import { useCallback, useState, type ReactNode } from 'react';
import Snackbar from '@mui/material/Snackbar';

/** How long a notice stays (mod-015 § 8.2, as mod-002's). */
const NOTICE_DURATION_MS = 6000;

/**
 * A page's one notice (spec § Dialog, § Page: a work item's form): «Рабочий
 * элемент <ID> создан.» on the list and «Изменения сохранены.» on the form.
 * Neither page is left after its notice, so each page hosts its own
 * `Snackbar`, 6 s at the bottom left; a newer notice replaces the shown one.
 */
export function useNotice(): { notice: ReactNode; show(text: string): void } {
  const [shown, setShown] = useState<{ id: number; text: string } | null>(null);
  const show = useCallback((text: string) => {
    setShown((current) => ({ id: (current?.id ?? 0) + 1, text }));
  }, []);

  const notice = shown && (
    <Snackbar
      key={shown.id}
      open
      autoHideDuration={NOTICE_DURATION_MS}
      // A click elsewhere — often the next action — does not dismiss it.
      onClose={(_event, reason) => {
        if (reason !== 'clickaway') {
          setShown(null);
        }
      }}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
      message={shown.text}
    />
  );
  return { notice, show };
}
