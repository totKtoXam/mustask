// The read's permission code (api-contract § 3, BR-M2-10). It is
// organization-scoped and in no token yet (D-8): the navigation entry
// declares it, and the page never checks it (design DD-6) — until mod-016 a
// non-member is answered by the API's not-found code, not by a refusal here.
export const processPermissions = {
  definitionRead: 'process.definition.read',
} as const;

export type ProcessPermission = (typeof processPermissions)[keyof typeof processPermissions];
