// Slice barrel (frontend.md "Slice rules"): what `app/` registers — the two
// pages and their routes for the router, and the landing builder and the read
// code for the navigation model's «Рабочие элементы» entry (spec
// § Navigation). `app/` imports this slice through this file only.
export { WorkItemFormPage } from './pages/WorkItemFormPage';
export { WorkItemsPage } from './pages/WorkItemsPage';
export { WORK_ITEMS_ROUTE, WORK_ITEM_ROUTE, workItemPath, workItemsPath } from './paths';
export { workItemsPermissions } from './permissions';
