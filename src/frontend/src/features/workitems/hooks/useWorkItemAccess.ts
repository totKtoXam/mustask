import { useAccessCheck } from '../../../shared/lib/access';
import { workItemsPermissions } from '../permissions';

/** What the signed-in user may do to the work items of the organization in the address. */
export interface WorkItemAccess {
  /** `workitems.workitem.write`: without it the card is read-only (mod-016 design § 6.5). */
  readOnly: boolean;
  /** `workitems.link.write`: without it «Родитель» is read-only on a card that is editable. */
  parentReadOnly: boolean;
}

/**
 * The role views of the work item card (mod-016 US-4; design § 6.2, § 6.5), asked of the
 * served access the organization gate binds to the address: a reader holds neither code,
 * a member holds both, and a bundle may hold the one without the other. A host of the card
 * — the page, the dialog over the backlog or the board — passes the two to `WorkItemCard`,
 * which itself asks nothing: it stays mounted through a change of role, so the draft the
 * user typed is still on screen when the codes go.
 */
export function useWorkItemAccess(): WorkItemAccess {
  const access = useAccessCheck();
  return {
    readOnly: !access(workItemsPermissions.workItemWrite, 'organization'),
    parentReadOnly: !access(workItemsPermissions.linkWrite, 'organization'),
  };
}
