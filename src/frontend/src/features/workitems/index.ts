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
export { AssigneeName, StateName, TypeName } from './components/Marks';
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
