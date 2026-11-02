import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { organizationErrorCodes } from '../../shared/api/errorCodes';
import { ApiError } from '../../shared/api/httpClient';
import type { WorkContextOffers } from '../../shared/lib/workContext';
import { readRemembered, writeRemembered } from './selection';
import { NO_SELECTION } from './selectorFit';
import { useNavigationSource } from './useNavigationSource';

// feat-043 QA, US-1 and US-8: an address that names an object which is gone or does not fit
// (BR-NV-7, BR-NV-10; ADR-0032 §§ 1, 3, 6). The slice's reads are stubbed as in
// useNavigationSource.test.tsx; the source is the real one.

type AreaStub = { state: 'pending' } | { state: 'gone' } | { state: 'node'; projectId: string };

const mocks = vi.hoisted(() => ({
  organizations: [] as { id: string; name: string }[],
  offers: undefined as unknown,
  area: { state: 'pending' } as AreaStub,
}));

vi.mock('../../features/organizations', async (importOriginal) => {
  const original = await importOriginal<typeof import('../../features/organizations')>();
  const done = <T,>(data: T) => ({
    data,
    isSuccess: true,
    isError: false,
    isFetching: false,
    refetch: vi.fn(),
  });
  return {
    ...original,
    useOrganizations: () => done(mocks.organizations),
    useSelectorOffers: () => done(mocks.offers),
    useArea: () => {
      const { area } = mocks;
      if (area.state === 'gone') {
        return {
          ...done(undefined),
          isSuccess: false,
          isError: true,
          error: new ApiError(404, {
            errorCode: organizationErrorCodes.areaNotFound,
          } as ConstructorParameters<typeof ApiError>[1]),
        };
      }
      if (area.state === 'node') {
        return done({ projectId: area.projectId });
      }
      return { ...done(undefined), isSuccess: false };
    },
    useProjectAreaTree: () => done([]),
  };
});

const USER = 'subject-1';
const ALFA = '10000000-0000-4000-8000-000000000001';
const MARS = '20000000-0000-4000-8000-000000000001';
const JUPITER = '20000000-0000-4000-8000-000000000002';
const GONE_TEAM = '20000000-0000-4000-8000-0000000000ff';
const DEPARTMENT = '40000000-0000-4000-8000-000000000001';
const OTHER_DEPARTMENT = '40000000-0000-4000-8000-000000000002';
const GONE_AREA = '40000000-0000-4000-8000-0000000000ff';
const NODE = '50000000-0000-4000-8000-000000000001';
const PROJECT_A = '30000000-0000-4000-8000-000000000001';
const GONE_PROJECT = '30000000-0000-4000-8000-0000000000ff';

const OFFERS: WorkContextOffers = {
  departments: [
    { id: DEPARTMENT, name: 'Логистика' },
    { id: OTHER_DEPARTMENT, name: 'Финансы' },
  ],
  teams: [
    { id: MARS, name: 'MARS', isMember: true, departmentIds: [] },
    { id: JUPITER, name: 'JUPITER', isMember: false, departmentIds: [] },
  ],
  projects: [{ id: PROJECT_A, name: 'A', key: 'A', departmentId: DEPARTMENT, teamIds: [MARS] }],
};

function render(path: string) {
  const wrapper = ({ children }: { children: ReactNode }) => (
    <MemoryRouter initialEntries={[path]}>{children}</MemoryRouter>
  );
  return renderHook(() => useNavigationSource({ enabled: true, subject: USER }), { wrapper });
}

beforeEach(() => {
  window.localStorage.clear();
  mocks.organizations = [{ id: ALFA, name: 'Альфа' }];
  mocks.offers = OFFERS;
  mocks.area = { state: 'pending' };
});

describe('feat-043 QA, US-1 and US-8: an address that names a gone object', () => {
  it('names the remembered object: it drops to «все» with the notice (BR-NV-7)', async () => {
    writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, project: GONE_PROJECT });

    const { result } = render(`/organizations/${ALFA}/projects/${GONE_PROJECT}/general`);

    await waitFor(() => expect(result.current.fallback).not.toBeNull());
    expect(result.current.fallback?.names).toEqual(['Альфа']);
    await waitFor(() =>
      expect(readRemembered(window.localStorage, USER, ALFA)).toMatchObject({ project: null }),
    );
  });

  // QA defect F-1 (qa.md), fixed: an address that names a gone object writes nothing.
  it('names another object than the remembered one: the context does not change, no notice', async () => {
    writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, team: MARS });

    const { result } = render(`/organizations/${ALFA}/projects/${GONE_PROJECT}/general`);

    // Let the source settle: the offers are ready at once, so one tick is enough.
    await waitFor(() => expect(result.current.selection.organizationId).toBe(ALFA));
    expect(result.current.fallback).toBeNull();
    expect(readRemembered(window.localStorage, USER, ALFA)).toMatchObject({ team: MARS });
  });

  // Code review C-4: only the gone kind drops, the other remembered values stay (US-8).
  it('drops only the gone kind of the remembered selection and keeps the rest', async () => {
    writeRemembered(window.localStorage, USER, ALFA, {
      ...NO_SELECTION,
      team: MARS,
      project: GONE_PROJECT,
    });

    const { result } = render(`/organizations/${ALFA}/projects/${GONE_PROJECT}/general`);

    await waitFor(() => expect(result.current.fallback).not.toBeNull());
    await waitFor(() =>
      expect(readRemembered(window.localStorage, USER, ALFA)).toEqual({
        ...NO_SELECTION,
        team: MARS,
      }),
    );
  });
});

describe('code review C-4: F-1 on the area prefix', () => {
  it('waits for the node: nothing is written while it loads', async () => {
    writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, team: MARS });
    mocks.area = { state: 'pending' };

    const { result } = render(`/organizations/${ALFA}/areas/${NODE}/overview`);

    await waitFor(() => expect(result.current.selection.organizationId).toBe(ALFA));
    expect(result.current.fallback).toBeNull();
    expect(readRemembered(window.localStorage, USER, ALFA)).toEqual({
      ...NO_SELECTION,
      team: MARS,
    });
  });

  it('a gone area that is not the remembered department writes nothing, no notice', async () => {
    writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, team: MARS });
    mocks.area = { state: 'gone' };

    const { result } = render(`/organizations/${ALFA}/areas/${GONE_AREA}/overview`);

    await waitFor(() => expect(result.current.selection.organizationId).toBe(ALFA));
    expect(result.current.fallback).toBeNull();
    expect(readRemembered(window.localStorage, USER, ALFA)).toEqual({
      ...NO_SELECTION,
      team: MARS,
    });
  });

  it('a gone area that is the remembered department drops it with the notice, keeps the team', async () => {
    writeRemembered(window.localStorage, USER, ALFA, {
      ...NO_SELECTION,
      department: GONE_AREA,
      team: MARS,
    });
    mocks.area = { state: 'gone' };

    const { result } = render(`/organizations/${ALFA}/areas/${GONE_AREA}/overview`);

    await waitFor(() => expect(result.current.fallback?.names).toEqual(['Альфа']));
    await waitFor(() =>
      expect(readRemembered(window.localStorage, USER, ALFA)).toEqual({
        ...NO_SELECTION,
        team: MARS,
      }),
    );
  });

  it('a node of an offered project sets the project and its department, and writes them', async () => {
    mocks.area = { state: 'node', projectId: PROJECT_A };

    const { result } = render(`/organizations/${ALFA}/areas/${NODE}/overview`);

    await waitFor(() => expect(result.current.selection.projectId).toBe(PROJECT_A));
    expect(result.current.selection.departmentId).toBe(DEPARTMENT);
    await waitFor(() =>
      expect(readRemembered(window.localStorage, USER, ALFA)).toEqual({
        department: DEPARTMENT,
        team: null,
        project: PROJECT_A,
      }),
    );
  });
});

describe('code review C-6: a selector in the query that names nothing or does not fit', () => {
  const base = `/organizations/${ALFA}/projects/${PROJECT_A}/general`;

  it('a gone team answers not found for the team, and the context is not written', async () => {
    const { result } = render(`${base}?teamId=${GONE_TEAM}`);

    await waitFor(() => expect(result.current.selectorNotFound).toBe('team'));
    expect(readRemembered(window.localStorage, USER, ALFA)).toBeNull();
  });

  it('a team that is not linked to the project answers not found for the team', async () => {
    const { result } = render(`${base}?teamId=${JUPITER}`);

    await waitFor(() => expect(result.current.selectorNotFound).toBe('team'));
    expect(readRemembered(window.localStorage, USER, ALFA)).toBeNull();
  });

  it('a department that does not hold the project answers not found for the department', async () => {
    const { result } = render(`${base}?departmentId=${OTHER_DEPARTMENT}`);

    await waitFor(() => expect(result.current.selectorNotFound).toBe('department'));
  });

  it('a team that fits answers nothing, and is written', async () => {
    const { result } = render(`${base}?teamId=${MARS}`);

    await waitFor(() => expect(result.current.selection.teamId).toBe(MARS));
    expect(result.current.selectorNotFound).toBeNull();
    await waitFor(() =>
      expect(readRemembered(window.localStorage, USER, ALFA)).toMatchObject({ team: MARS }),
    );
  });

  it('the object of the prefix answers first: a gone project says nothing of its team', async () => {
    const { result } = render(
      `/organizations/${ALFA}/projects/${GONE_PROJECT}/general?teamId=${GONE_TEAM}`,
    );

    await waitFor(() => expect(result.current.selection.organizationId).toBe(ALFA));
    expect(result.current.selectorNotFound).toBeNull();
  });

  it('a gone project in the settings query answers not found for the project', async () => {
    const { result } = render(`/organizations/${ALFA}/settings/general?projectId=${GONE_PROJECT}`);

    await waitFor(() => expect(result.current.selectorNotFound).toBe('project'));
  });

  it('a gone remembered team that the address names drops it, and the page is not found', async () => {
    writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, team: GONE_TEAM });

    const { result } = render(`${base}?teamId=${GONE_TEAM}`);

    await waitFor(() => expect(result.current.selectorNotFound).toBe('team'));
    await waitFor(() => expect(result.current.fallback?.names).toEqual(['Альфа']));
    await waitFor(() =>
      expect(readRemembered(window.localStorage, USER, ALFA)).toMatchObject({ team: null }),
    );
  });
});
