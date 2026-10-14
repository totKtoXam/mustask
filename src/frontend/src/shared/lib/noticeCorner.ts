import { createContext, useContext, useState } from 'react';
import { useWorkContext } from './workContext';

/**
 * The bottom-left corner holds one notice at a time (mod-002 DC-7, mod-016
 * DC-11). The shell owns two of the notices that compete for it — the
 * switcher's fallback notice and the role-change notice — and three slice
 * hosts own the page's own (organizations, work items, boards). Instead of
 * one shared corner, the shell provides a signal and each host reads it
 * (plan-frontend P-6):
 *
 * - the host closes its notice when the shell raises a new fallback (read
 *   from the work context) or a new role change (this context's `roleChangeId`);
 * - the host calls `claim()` when it shows a notice, so a newer notice of the
 *   page replaces the role-change notice, which says why a control went and
 *   is not worth more than the action the user has just taken.
 *
 * The fallback notice wins over both: while it is shown the shell raises no
 * role change.
 */
export interface NoticeCorner {
  /** The id of the role-change notice on screen; `null`: none. */
  roleChangeId: number | null;
  /** A host shows a notice: the role-change notice, if any, closes. */
  claim(): void;
}

const NO_CORNER: NoticeCorner = { roleChangeId: null, claim: () => {} };

/** Inert outside the shell: a page rendered alone has no role-change notice. */
export const NoticeCornerContext = createContext<NoticeCorner>(NO_CORNER);

/**
 * For a slice's notice host: pass the function that closes its own notice;
 * the hook calls it when a new fallback or a new role-change notice is
 * raised, and returns `claim`, to call whenever the host shows a notice.
 */
export function useNoticeHost(close: () => void): () => void {
  const fallbackId = useWorkContext().fallback?.id ?? null;
  const { roleChangeId, claim } = useContext(NoticeCornerContext);
  const [seen, setSeen] = useState({ fallbackId, roleChangeId });
  if (seen.fallbackId !== fallbackId || seen.roleChangeId !== roleChangeId) {
    setSeen({ fallbackId, roleChangeId });
    const fallbackRaised = fallbackId !== null && fallbackId !== seen.fallbackId;
    const roleChangeRaised = roleChangeId !== null && roleChangeId !== seen.roleChangeId;
    if (fallbackRaised || roleChangeRaised) {
      close();
    }
  }
  return claim;
}
