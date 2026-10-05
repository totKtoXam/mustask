// The slice's two addresses (spec § Screens and copy; status.md D-5), under
// mod-015 BR-18's project prefix. Ids and the identifier are untyped route
// parameters: a page never parses or validates them, and the builders
// re-encode them as received, as mod-002's do.

export const WORK_ITEMS_ROUTE = '/organizations/:organizationId/projects/:projectId/workitems';
export const WORK_ITEM_ROUTE = `${WORK_ITEMS_ROUTE}/:identifier`;

const segment = encodeURIComponent;

/** «Рабочие элементы» of a project: the navigation entry's landing. */
export function workItemsPath(organizationId: string, projectId: string): string {
  return `/organizations/${segment(organizationId)}/projects/${segment(projectId)}/workitems`;
}

/** A work item's form, by its identifier inside the addressed project (D-5). */
export function workItemPath(
  organizationId: string,
  projectId: string,
  identifier: string,
): string {
  return `${workItemsPath(organizationId, projectId)}/${segment(identifier)}`;
}
