import { createContext } from 'react';

/** `plain` is the notice's own message; `success` and `error` are an `Alert` of that severity. */
export type NoticeSeverity = 'plain' | 'success' | 'error';

export interface NoticeApi {
  /**
   * Shows a short result message — «Изменения сохранены.» — bottom left. One at a time: a
   * newer notice replaces the shown one, and the same text again is announced again.
   */
  show: (text: string, severity?: NoticeSeverity) => void;
}

/** Null outside a `NoticeProvider`; `useNotice` refuses that. */
export const NoticeContext = createContext<NoticeApi | null>(null);
