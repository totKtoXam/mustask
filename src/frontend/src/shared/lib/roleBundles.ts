// The panel's one reading of the role-bundle contract (mod-019 api-contract § 9, ADR-0033, ADR-0038):
// `contracts/access/role-bundles.json` is generated from the domain by the backend's contract test, and
// the panel imports it at build time instead of keeping a hand copy of the tables (tech-026 OP-7).
import bundles from '../../../../../contracts/access/role-bundles.json';

/** Where a code is decided: the organization, or one object kind (ADR-0029 § 2 as amended by ADR-0038). */
export type ResourceScope = 'organization' | 'area' | 'project' | 'team';

/** The role of a grant, the same word in every scope (api-contract § 0). */
export type GrantRoleCode = 'reader' | 'member' | 'manager';

interface CatalogEntry {
  code: string;
  source: 'instance' | 'organization';
  resourceScope: ResourceScope | null;
}

interface RoleBundlesContract {
  instanceRoles: Record<string, string[]>;
  organizationRoles: Record<'admin' | 'member' | 'viewer', string[]>;
  scopedRoles: Record<'area' | 'project' | 'team', Record<GrantRoleCode, string[]>>;
  catalog: CatalogEntry[];
}

export const roleBundles = bundles as RoleBundlesContract;

const scopeByCode = new Map<string, ResourceScope | null>(
  roleBundles.catalog.map((entry) => [entry.code, entry.resourceScope]),
);

/**
 * The resource scope a code is decided at; `null` for an instance code (it travels in the token) and
 * for a code the catalog does not know.
 */
export function resourceScopeOf(code: string): ResourceScope | null {
  return scopeByCode.get(code) ?? null;
}

/** Whether the organization answer carries the code (BR-SA-18): everything else is read off the object. */
export function isOrganizationScope(code: string): boolean {
  return resourceScopeOf(code) === 'organization';
}
