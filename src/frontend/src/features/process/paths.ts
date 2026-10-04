// The slice's one address: the read-only process screen of an organization
// (mod-001 D-9, mod-015 D-17). The id is an untyped route parameter: the
// screen never parses or validates it (design DD-4).
export const PROCESS_DEFINITION_ROUTE = '/organizations/:organizationId/settings/process';

export function processDefinitionPath(organizationId: string): string {
  return `/organizations/${encodeURIComponent(organizationId)}/settings/process`;
}
