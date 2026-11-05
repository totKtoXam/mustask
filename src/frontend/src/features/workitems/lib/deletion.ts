import { formatDateTime } from '../../../shared/lib/format';
import type { WorkItem, WorkItemDeletion, WorkItemUserReference } from '../api/workItemsApi';
import { UNKNOWN_USER_LABEL } from './assignee';
import { deletionTexts } from './cardTexts';

/**
 * The category of «Удалена» (mod-005 BR-DR-1; doc-001 BR-2): the category decides whether an
 * item is deleted, never the state's name and never the item's `deletion` marks — an item put
 * there outside Delete has none (BR-DR-10).
 */
export const REMOVED_CATEGORY = 'Removed';

/** Whether the item is in «Удалена» (design § 4: the mode follows the read). */
export function isDeleted(item: Pick<WorkItem, 'state'>): boolean {
  return item.state.category.code === REMOVED_CATEGORY;
}

/**
 * A user named with the marks BR-13 gives: «Анна Смирнова (неактивен)» for an account that is
 * disabled or deleted, «(не в организации)» for one that has left the organization, which
 * prevails. A user the directory does not know reads «Неизвестный пользователь» and carries
 * no «неактивен» — the contract flags an unknown user inactive, which says nothing more than
 * the name does — but still «не в организации» when the membership says so.
 */
export function userNameWithMarks(
  user: Pick<WorkItemUserReference, 'displayName' | 'isActive' | 'isMember'>,
): string {
  const name = user.displayName ?? UNKNOWN_USER_LABEL;
  if (!user.isMember) {
    return `${name} (${deletionTexts.notMemberMark})`;
  }
  if (!user.isActive && user.displayName !== null) {
    return `${name} (${deletionTexts.inactiveMark})`;
  }
  return name;
}

/**
 * The card's second line of the deleted notice (BR-DR-7): who deleted the item and when;
 * `null` for an item with no marks — it shows no such line and keeps «Восстановить».
 */
export function deletionLine(deletion: WorkItemDeletion | null | undefined): string | null {
  if (!deletion) {
    return null;
  }
  return deletionTexts.line(userNameWithMarks(deletion.by), formatDateTime(deletion.at));
}
