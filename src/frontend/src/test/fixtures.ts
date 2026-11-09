// Fixture factories with one id scheme (tech-035, docs/conventions/testing.md § 3).
// An id is `<kind letter>0000000-0000-4000-8000-<n, 12 digits>`: a canonical lowercase uuid whose first
// letter says what it identifies, so a failing assertion shows that `a…0001` is an organization. It is
// the scheme `ids` of `features/organizations/testFixtures.ts` already follows (`ids.alfa` is
// organization 1).
import type {
  Dataset,
  OrganizationRecord,
  ProjectRecord,
  TeamRecord,
} from '../features/organizations/testFixtures';

const KIND_LETTER = {
  organization: 'a',
  project: 'b',
  team: 'c',
  area: 'd',
  user: 'e',
} as const;

export type FixtureKind = keyof typeof KIND_LETTER;

/** The id of the `n`-th (1-based) thing of `kind`; the same arguments give the same id. */
export function fixtureId(kind: FixtureKind, n: number): string {
  if (!Number.isInteger(n) || n < 1) {
    throw new Error(`A fixture is numbered from 1, got ${n}.`);
  }
  return `${KIND_LETTER[kind]}0000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
}

/** An active organization; the name follows the number unless given. */
export function organizationOf(
  n: number,
  overrides: Partial<OrganizationRecord> = {},
): OrganizationRecord {
  return { id: fixtureId('organization', n), name: `Организация ${n}`, active: true, ...overrides };
}

/** An active project of an organization (its record or its id). */
export function projectOf(
  n: number,
  organization: OrganizationRecord | string,
  overrides: Partial<ProjectRecord> = {},
): ProjectRecord {
  return {
    id: fixtureId('project', n),
    organizationId: typeof organization === 'string' ? organization : organization.id,
    name: `Проект ${n}`,
    key: `PR${n}`,
    active: true,
    ...overrides,
  };
}

/** A live team of an organization (its record or its id). */
export function teamOf(
  n: number,
  organization: OrganizationRecord | string,
  overrides: Partial<TeamRecord> = {},
): TeamRecord {
  return {
    id: fixtureId('team', n),
    organizationId: typeof organization === 'string' ? organization : organization.id,
    name: `Команда ${n}`,
    description: null,
    deleted: false,
    ...overrides,
  };
}

/** A dataset of exactly what the test names; everything else is empty. */
export function datasetOf(parts: Partial<Dataset> = {}): Dataset {
  return { organizations: [], projects: [], teams: [], links: [], areas: [], ...parts };
}
