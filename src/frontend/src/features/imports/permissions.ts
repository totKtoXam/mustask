// The module's organization-scope read code (api-contract § 0): every member holds it, and the page
// gate asks it. The write and run rights are never asked here: the server serves `canCreate`,
// `canRun` and the per-row flags, because a manager's right sits on a project (BR-IE-18).
export const importsPermissions = {
  templateRead: 'imports.template.read',
} as const;

export type ImportsPermission = (typeof importsPermissions)[keyof typeof importsPermissions];
