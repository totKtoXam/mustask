// The slice's addresses: «Импорт» of an organization's settings (design § 1). The ids are untyped
// route parameters the pages validate by the server's answer.
export const IMPORTS_ROUTE = '/organizations/:organizationId/settings/imports';
export const IMPORT_TEMPLATE_NEW_ROUTE = `${IMPORTS_ROUTE}/templates/new`;
export const IMPORT_TEMPLATE_ROUTE = `${IMPORTS_ROUTE}/templates/:templateId`;
export const IMPORT_CHECK_ROUTE = `${IMPORTS_ROUTE}/check`;

const encoded = encodeURIComponent;

export function importsPath(organizationId: string): string {
  return `/organizations/${encoded(organizationId)}/settings/imports`;
}

export function newTemplatePath(organizationId: string): string {
  return `${importsPath(organizationId)}/templates/new`;
}

export function templatePath(organizationId: string, templateId: string): string {
  return `${importsPath(organizationId)}/templates/${encoded(templateId)}`;
}

/** The check page, optionally with a template chosen (`?template=`). */
export function checkPath(organizationId: string, templateId?: string): string {
  const base = `${importsPath(organizationId)}/check`;
  return templateId ? `${base}?template=${encoded(templateId)}` : base;
}
