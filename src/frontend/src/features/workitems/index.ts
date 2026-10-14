// Slice barrel (frontend.md "Slice rules"): what `app/` registers — the two
// pages and their routes for the router, and the landing builder and the read
// code for the navigation model's «Рабочие элементы» entry (spec
// § Navigation). `app/` imports this slice through this file only.
export { WorkItemFormPage } from './pages/WorkItemFormPage';
export { WorkItemsPage } from './pages/WorkItemsPage';
export { WORK_ITEMS_ROUTE, WORK_ITEM_ROUTE, workItemPath, workItemsPath } from './paths';
export { workItemsPermissions } from './permissions';
// mod-006 creates from a team's backlog or board with this slice's dialog,
// picker and create, moves a card with this slice's save of the state (its
// spec § Dialog; api-contract § 6, § 7), and draws a row's or a card's type,
// state and assignee as this slice's list does.
export { CreateWorkItemDialog, type CreateDialogTeam } from './components/CreateWorkItemDialog';
export { AssigneeAvatar, AssigneeName, StateName, TypeName } from './components/Marks';
// feat-032: a board card's checklist row shows the assignee as an avatar alone, named by the
// same words as the list's «Исполнитель».
export { UNKNOWN_USER_LABEL, assigneeLabel } from './lib/assignee';
export { NEW_WORK_ITEM_LABEL, TypePickerMenu } from './components/TypePickerMenu';
export { useCreateWorkItem, useSetWorkItemState } from './hooks/mutations';
// feat-001 frontend pass B: mod-006's backlog maps a row's parent one level up
// with this slice's parent write and its «Родитель» picker (BR-H-14), and
// links a row's «Родитель» to the parent's form.
export { ParentPicker } from './components/ParentPicker';
export { useSetWorkItemParent } from './hooks/mutations';
export type { CreatableType } from './lib/process';
export type {
  ParentChange,
  ParentWriteResult,
  WorkItem,
  WorkItemAssignee,
  WorkItemRef,
  WorkItemStateReference,
  WorkItemTypeReference,
} from './api/workItemsApi';
// feat-027: the backlog's inline add row makes mod-003's create itself — with
// the same one `requestId` per submission, the same title check and the same
// placing of a refusal — so the dialog's own helpers are shared through the barrel.
export { newRequestId } from './lib/requestId';
export { placeRefusal, type PlacedRefusal } from './lib/refusals';
export { titleError } from './lib/rules';
// feat-027 US-6: the backlog's «Изменить родителя…» offers every parent type
// BR-H-2 allows and names them in its helper, as the form does.
export { allowedParentTypes, parentHelperText } from './lib/process';
// feat-029: the dialog's place in the address (`?workitem=<ID>`), which the backlog and
// the board read to open the card over themselves (BR-C-1, BR-C-2).
export { WORK_ITEM_PARAM, useWorkItemDialog } from './hooks/useWorkItemDialog';
// feat-029 pass B: the backlog and the board mount the card's dialog once and open it from
// a title — a plain click only; a modified click follows the title's `href`.
export { WorkItemCardDialog } from './components/WorkItemCardDialog';
export { isPlainClick } from './lib/plainClick';
