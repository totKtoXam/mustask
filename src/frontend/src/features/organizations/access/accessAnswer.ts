import type { OrganizationRole } from '../lib/roles';

/**
 * What the tab holds for one organization's access (mod-016 design § 2.1):
 * the codes of the last read that answered 200, or the 404 «Организация не
 * найдена» that the access read, or a scoped call of a page, answered. Nothing
 * held is `undefined` (no answer yet, or only failed ones).
 */
export type AccessAnswer =
  | { kind: 'member'; role: OrganizationRole; permissions: readonly string[] }
  | { kind: 'not-member' };

/** The five exclusive states an organization page is in (design § 2.1). */
export type GateRow = 'loading' | 'failed' | 'notFound' | 'denied' | 'content';

/** What the gate reads of the access query. */
export interface GateInput {
  /** The held answer. */
  answer: AccessAnswer | undefined;
  /** A read is running. */
  fetching: boolean;
  /** The last read failed (not 404). */
  failed: boolean;
}

/**
 * The state of the page under the gate, from what the tab holds (design
 * § 2.1). The rows are exclusive and the order of the conditions is the
 * design's:
 *
 * - a held 404 is row 3 whatever else is running or has failed since: the
 *   frame holds through re-reads that fail or answer 404 again, and a re-read
 *   that answers 200 replaces it;
 * - held codes are row 4 or 5 by whether the page's code is among them; a
 *   re-read, running or failed, changes nothing;
 * - nothing held is row 1 while a read is running or has not started, and row
 *   2 once the last read failed and none is running.
 *
 * Row 1 never shows «Доступ ограничен»: unknown is not denied (BR-M1-31).
 */
export function gateRow({ answer, fetching, failed }: GateInput, holds: boolean): GateRow {
  if (answer?.kind === 'not-member') {
    return 'notFound';
  }
  if (answer?.kind === 'member') {
    return holds ? 'content' : 'denied';
  }
  return failed && !fetching ? 'failed' : 'loading';
}
