import { useEffect } from 'react';
import { PRODUCT_NAME } from '../config/product';

/** «<title> — Mustask», or «Mustask» alone when there is no page title (BR-6). */
export function formatDocumentTitle(title: string | null): string {
  return title ? `${title} — ${PRODUCT_NAME}` : PRODUCT_NAME;
}

/**
 * Sets the browser tab's title while the caller is mounted with this title.
 * `undefined` leaves the tab alone — for a component that sets it only in
 * some of its states (a state frame that does not replace the whole page).
 */
export function useDocumentTitle(title: string | null | undefined): void {
  useEffect(() => {
    if (title === undefined) {
      return;
    }
    document.title = formatDocumentTitle(title);
  }, [title]);
}
