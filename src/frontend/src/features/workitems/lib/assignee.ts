import type { WorkItemAssignee } from '../api/workItemsApi';

/** The empty assignee's words, in the list, the form and the picker's first option (BR-M3-22). */
export const UNASSIGNED_LABEL = 'Не назначен';

/** An assignee the user directory does not know (api-contract § 1.1; spec § Page «Рабочие элементы»). */
export const UNKNOWN_USER_LABEL = 'Неизвестный пользователь';

/**
 * How an assignee reads: «Не назначен» for none, the display name for a user
 * the directory knows, «Неизвестный пользователь» when it does not — then
 * `displayName` is `null`. No mark for an inactive user (D-2).
 */
export function assigneeLabel(assignee: WorkItemAssignee | null): string {
  if (assignee === null) {
    return UNASSIGNED_LABEL;
  }
  return assignee.displayName ?? UNKNOWN_USER_LABEL;
}
