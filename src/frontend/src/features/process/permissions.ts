// The read's permission code (api-contract § 3, BR-M2-10; mod-016
// api-contract § 0.2). It is organization-scoped and in no token: the panel
// reads it from `GET …/access` (ADR-0027). The navigation entry declares it,
// and the organization gate asks it for the page (mod-016 design § 2.1) — the
// page itself makes no check (design DD-6): a non-member gets «Организация не
// найдена», a member without the code «Доступ ограничен», from the wrapper.
export const processPermissions = {
  definitionRead: 'process.definition.read',
} as const;

export type ProcessPermission = (typeof processPermissions)[keyof typeof processPermissions];
