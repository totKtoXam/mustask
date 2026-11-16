import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { organizationErrorCodes } from '../../shared/api/errorCodes';
import { ApiError } from '../../shared/api/httpClient';
import type { WorkContextOffers } from '../../shared/lib/workContext';
import { organizationStorageKey, readRemembered, writeRemembered } from './selection';
import { NO_SELECTION } from './selectorFit';
import { useNavigationSource } from './useNavigationSource';

// feat-043 US-8, ADR-0032 § 6: the remembered selection and the fallback notice, at the seam
// where the shell's source meets the stored selection. The slice's reads are stubbed; the
// router, the storage and the source's own logic are the real ones. The QA and code review
// cases (a gone or misfitting object in the address) share this file's one mock of the slice:
// a module is mocked by one test file (tech-029).

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
const BETA = '10000000-0000-4000-8000-000000000002';
const MARS = '20000000-0000-4000-8000-000000000001';
const VENUS = '20000000-0000-4000-8000-000000000002';
const JUPITER = '20000000-0000-4000-8000-000000000003';
const GONE_TEAM = '20000000-0000-4000-8000-0000000000ff';
const DEPARTMENT = '40000000-0000-4000-8000-000000000001';
const OTHER_DEPARTMENT = '40000000-0000-4000-8000-000000000002';
const GONE_AREA = '40000000-0000-4000-8000-0000000000ff';
const NODE = '50000000-0000-4000-8000-000000000001';
const PROJECT_A = '30000000-0000-4000-8000-000000000001';
const GONE_PROJECT = '30000000-0000-4000-8000-0000000000ff';

const OFFERS: WorkContextOffers = {
  departments: [],
  teams: [
    { id: MARS, name: 'MARS', isMember: true, departmentId: 'd1' },
    { id: VENUS, name: 'VENUS', isMember: false, departmentId: 'd1' },
  ],
  projects: [{ id: PROJECT_A, name: 'A', key: 'A', departmentId: null, teamIds: [MARS, VENUS] }],
};

// The QA cases' offers: departments, and a project that sits in one.
const QA_OFFERS: WorkContextOffers = {
  departments: [
    { id: DEPARTMENT, name: 'Логистика' },
    { id: OTHER_DEPARTMENT, name: 'Финансы' },
  ],
  teams: [
    { id: MARS, name: 'MARS', isMember: true, departmentId: 'd1' },
    { id: JUPITER, name: 'JUPITER', isMember: false, departmentId: 'd1' },
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
  mocks.organizations = [
    { id: ALFA, name: 'Альфа' },
    { id: BETA, name: 'Бета' },
  ];
  mocks.offers = OFFERS;
  mocks.area = { state: 'pending' };
});

describe('feat-043 US-8: the remembered selection applies to pages without an organization', () => {
  it('reads the remembered organization and objects on a page that names none', async () => {
    writeRemembered(window.localStorage, USER, BETA, { ...NO_SELECTION, team: MARS });

    const { result } = render('/organizations');

    await waitFor(() => expect(result.current.selection.teamId).toBe(MARS));
    expect(result.current.selection.organizationId).toBe(BETA);
    expect(result.current.fallback).toBeNull();
  });

  it('lets an address with an organization decide the selection and writes it down', async () => {
    writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, team: VENUS });

    const { result } = render(`/organizations/${ALFA}/teams/${MARS}/overview`);

    await waitFor(() => expect(result.current.selection.teamId).toBe(MARS));
    expect(result.current.selection.organizationId).toBe(ALFA);
    await waitFor(() =>
      expect(readRemembered(window.localStorage, USER, ALFA)).toMatchObject({ team: MARS }),
    );
  });

  it('starts at the first organization with nothing chosen when nothing is remembered', async () => {
    const { result } = render('/organizations');

    await waitFor(() => expect(result.current.selection.organizationId).toBe(ALFA));
    expect(result.current.selection.teamId).toBeUndefined();
    expect(result.current.fallback).toBeNull();
  });
});

describe('feat-043 US-8, ADR-0032 § 6: an organization page with no object reads the remembered selection', () => {
  const MEMBERS = `/organizations/${ALFA}/members`;

  it('reads the remembered objects from the address, else the remembered ones (reload on «Участники»)', async () => {
    writeRemembered(window.localStorage, USER, ALFA, {
      ...NO_SELECTION,
      team: MARS,
      project: PROJECT_A,
    });

    const { result } = render(MEMBERS);

    await waitFor(() => expect(result.current.selection.teamId).toBe(MARS));
    expect(result.current.selection.projectId).toBe(PROJECT_A);
    expect(result.current.selection.organizationId).toBe(ALFA);
    expect(result.current.fallback).toBeNull();
  });

  it('is not overwritten by the visit: the selection stays as it was remembered', async () => {
    const remembered = { ...NO_SELECTION, team: MARS, project: PROJECT_A };
    writeRemembered(window.localStorage, USER, BETA, remembered);

    const { result } = render(`/organizations/${ALFA}/overview`);

    await waitFor(() => expect(result.current.selection.organizationId).toBe(ALFA));
    expect(readRemembered(window.localStorage, USER, ALFA)).toBeNull();
    expect(readRemembered(window.localStorage, USER, BETA)).toEqual(remembered);
    // The visit notes the last organization and nothing else.
    expect(window.localStorage.getItem(organizationStorageKey(USER))).toBe(ALFA);
  });

  it('an address that names an object wins over the remembered selection and replaces it', async () => {
    writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, team: VENUS });

    const { result } = render(`/organizations/${ALFA}/projects/${PROJECT_A}/general`);

    await waitFor(() => expect(result.current.selection.projectId).toBe(PROJECT_A));
    expect(result.current.selection.teamId).toBeUndefined();
    await waitFor(() =>
      expect(readRemembered(window.localStorage, USER, ALFA)).toEqual({
        ...NO_SELECTION,
        project: PROJECT_A,
      }),
    );
  });

  it('«все» chosen is remembered, so it stays chosen on such a page after a reload', async () => {
    writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, team: MARS });

    const { result, rerender } = render(MEMBERS);
    await waitFor(() => expect(result.current.selection.teamId).toBe(MARS));

    act(() => result.current.remember?.(ALFA, NO_SELECTION));
    rerender();

    await waitFor(() => expect(result.current.selection.teamId).toBeUndefined());
    expect(readRemembered(window.localStorage, USER, ALFA)).toEqual(NO_SELECTION);
  });

  it('drops a remembered object that is gone, with the notice, on such a page too', async () => {
    const gone = '20000000-0000-4000-8000-0000000000ff';
    writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, team: gone });

    const { result } = render(MEMBERS);

    await waitFor(() => expect(result.current.fallback?.names).toEqual(['Альфа']));
    expect(result.current.selection.teamId).toBeUndefined();
    await waitFor(() =>
      expect(readRemembered(window.localStorage, USER, ALFA)).toMatchObject({ team: null }),
    );
  });
});

describe('feat-043 US-8, ADR-0032 § 6: the fallback notice', () => {
  it('drops a remembered object that is gone to «все», says so once and repairs the store', async () => {
    const gone = '20000000-0000-4000-8000-0000000000ff';
    writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, team: gone });

    const { result } = render('/organizations');

    await waitFor(() => expect(result.current.fallback).not.toBeNull());
    expect(result.current.fallback?.names).toEqual(['Альфа']);
    expect(result.current.selection.teamId).toBeUndefined();
    await waitFor(() =>
      expect(readRemembered(window.localStorage, USER, ALFA)).toMatchObject({ team: null }),
    );

    act(() => result.current.dismissFallback?.());
    await waitFor(() => expect(result.current.fallback).toBeNull());
  });

  it('falls back to the first organization when the remembered one is gone', async () => {
    writeRemembered(window.localStorage, USER, '10000000-0000-4000-8000-0000000000ff', {
      ...NO_SELECTION,
      team: MARS,
    });

    const { result } = render('/organizations');

    await waitFor(() => expect(result.current.fallback).not.toBeNull());
    expect(result.current.selection.organizationId).toBe(ALFA);
    expect(result.current.fallback?.names).toEqual(['Альфа']);
    expect(result.current.selection.teamId).toBeUndefined();
  });

  it('says only that the context is gone when no organization is left', async () => {
    mocks.organizations = [];
    writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, team: MARS });

    const { result } = render('/organizations');

    await waitFor(() => expect(result.current.fallback).not.toBeNull());
    expect(result.current.fallback?.names).toEqual([]);
    expect(result.current.selection.organizationId).toBeUndefined();
  });
});

function useQaOffers() {
  mocks.organizations = [{ id: ALFA, name: 'Альфа' }];
  mocks.offers = QA_OFFERS;
}

describe('feat-043 QA, US-1 and US-8: an address that names a gone object', () => {
  beforeEach(useQaOffers);

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
    // D-8, BR-NV-7: the selectors do not change either.
    expect(result.current.selection.teamId).toBe(MARS);
    expect(result.current.selection.projectId).toBeUndefined();
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
  beforeEach(useQaOffers);

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
  beforeEach(useQaOffers);

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

  // D-1: mod-017's filter value «Без команды» is no team, whichever order the values come in.
  it.each(['?teamId=none', `?teamId=none&teamId=${MARS}`, `?teamId=${MARS}&teamId=none`])(
    'the filter value none at %s is no team: not found never answers for it',
    async (query) => {
      const { result } = render(`${base}${query}`);

      await waitFor(() => expect(result.current.selection.projectId).toBe(PROJECT_A));
      expect(result.current.selectorNotFound).toBeNull();
      expect(result.current.selection.teamId).toBe(query === '?teamId=none' ? undefined : MARS);
    },
  );

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

// A link change: project A was linked to MARS and VENUS, then to VENUS alone.
const UNLINKED: WorkContextOffers = {
  ...OFFERS,
  projects: [{ id: PROJECT_A, name: 'A', key: 'A', departmentId: null, teamIds: [VENUS] }],
};

describe('feat-043 D-4: a link change is not a not found', () => {
  const address = `/organizations/${ALFA}/projects/${PROJECT_A}/teams?teamId=${MARS}`;

  it('is not `selectorNotFound` for a pair that fitted the offers before, and writes nothing', async () => {
    const { result, rerender } = render(address);
    await waitFor(() => expect(result.current.selection.teamId).toBe(MARS));
    expect(result.current.selectorNotFound).toBeNull();

    mocks.offers = UNLINKED;
    rerender();

    // The very render that holds the new offers: the shell resets the pair next.
    expect(result.current.offers).toBe(UNLINKED);
    expect(result.current.selectorNotFound).toBeNull();
    expect(readRemembered(window.localStorage, USER, ALFA)).toMatchObject({
      team: MARS,
      project: PROJECT_A,
    });
  });

  it('is still `selectorNotFound` for a pair that never fitted, whatever the offers do after', async () => {
    mocks.offers = UNLINKED;
    const { result, rerender } = render(address);
    await waitFor(() => expect(result.current.selectorNotFound).toBe('team'));

    mocks.offers = { ...UNLINKED, projects: [...UNLINKED.projects] };
    rerender();

    expect(result.current.selectorNotFound).toBe('team');
  });
});

describe('feat-043 D-5: a remembered pair that stopped fitting repairs itself on read', () => {
  const pair = { ...NO_SELECTION, team: MARS, project: PROJECT_A };

  it('drops the more specific value, says so once and repairs the store, on «Участники»', async () => {
    mocks.offers = UNLINKED;
    writeRemembered(window.localStorage, USER, ALFA, pair);

    const { result } = render(`/organizations/${ALFA}/members`);

    await waitFor(() => expect(result.current.fallback?.names).toEqual(['Альфа']));
    expect(result.current.selection.teamId).toBe(MARS);
    expect(result.current.selection.projectId).toBeUndefined();
    await waitFor(() =>
      expect(readRemembered(window.localStorage, USER, ALFA)).toEqual({
        ...NO_SELECTION,
        team: MARS,
      }),
    );
    expect(result.current.fallback?.id).toBe(1);
  });

  it('does the same on a page with no organization', async () => {
    mocks.offers = UNLINKED;
    writeRemembered(window.localStorage, USER, ALFA, pair);

    const { result } = render('/organizations');

    await waitFor(() => expect(result.current.fallback).not.toBeNull());
    expect(result.current.selection.projectId).toBeUndefined();
    expect(result.current.selection.teamId).toBe(MARS);
  });

  it('gives no menu entry a pair its address answers not found for', async () => {
    mocks.offers = UNLINKED;
    writeRemembered(window.localStorage, USER, ALFA, pair);

    const { result } = render(`/organizations/${ALFA}/members`);

    await waitFor(() => expect(result.current.fallback).not.toBeNull());
    const link = result.current.objectLink?.('project', PROJECT_A);
    expect(link?.to).not.toContain(`teamId=${MARS}`);
  });

  it('says nothing for a pair that still fits', async () => {
    writeRemembered(window.localStorage, USER, ALFA, pair);

    const { result } = render(`/organizations/${ALFA}/members`);

    await waitFor(() => expect(result.current.selection.projectId).toBe(PROJECT_A));
    expect(result.current.fallback).toBeNull();
  });

  it('leaves a link change after the first read to the shell: the pair stays until it resets it', async () => {
    writeRemembered(window.localStorage, USER, ALFA, pair);
    const { result, rerender } = render(`/organizations/${ALFA}/members`);
    await waitFor(() => expect(result.current.selection.projectId).toBe(PROJECT_A));

    mocks.offers = UNLINKED;
    rerender();

    expect(result.current.selection.projectId).toBe(PROJECT_A);
    expect(result.current.fallback).toBeNull();
    expect(readRemembered(window.localStorage, USER, ALFA)).toEqual(pair);
  });
});

describe('feat-043 D-7: a choice written by the shell is read back at once', () => {
  it('moves the selectors of a page that names no object, with no navigation', async () => {
    writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, team: MARS });
    const { result } = render('/organizations');
    await waitFor(() => expect(result.current.selection.teamId).toBe(MARS));

    act(() => result.current.remember?.(ALFA, { ...NO_SELECTION, team: VENUS }));

    await waitFor(() => expect(result.current.selection.teamId).toBe(VENUS));
  });
});

describe('feat-043 D-6: a node or sub-area link keeps the team that fits', () => {
  const SUB = '50000000-0000-4000-8000-0000000000aa';

  it('rides the fitting team in the query, on the area prefix', async () => {
    const { result } = render(`/organizations/${ALFA}/teams/${MARS}/overview`);
    await waitFor(() => expect(result.current.selection.teamId).toBe(MARS));

    const link = result.current.objectLink?.('project', PROJECT_A, SUB);

    expect(link?.to).toBe(`/organizations/${ALFA}/areas/${SUB}/overview?teamId=${MARS}`);
    expect(link?.notice).toBeNull();
  });

  it('drops a team the project is not linked to, with the reset notice', async () => {
    mocks.offers = UNLINKED;
    const { result } = render(`/organizations/${ALFA}/teams/${MARS}/overview`);
    await waitFor(() => expect(result.current.selection.teamId).toBe(MARS));

    const link = result.current.objectLink?.('project', PROJECT_A, SUB);

    expect(link?.to).toBe(`/organizations/${ALFA}/areas/${SUB}/overview`);
    expect(link?.notice).toMatch(/Выбор сброшен/);
  });
});
