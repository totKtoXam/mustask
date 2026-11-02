import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { WorkContextOffers } from '../../shared/lib/workContext';
import { readRemembered, writeRemembered } from './selection';
import { NO_SELECTION } from './selectorFit';
import { useNavigationSource } from './useNavigationSource';

// feat-043 US-8, ADR-0032 § 6: the remembered selection and the fallback notice, at the seam
// where the shell's source meets the stored selection. The slice's reads are stubbed; the
// router, the storage and the source's own logic are the real ones.

const mocks = vi.hoisted(() => ({
  organizations: [] as { id: string; name: string }[],
  offers: undefined as unknown,
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
    useArea: () => ({ ...done(undefined), isSuccess: false }),
    useProjectAreaTree: () => done([]),
  };
});

const USER = 'subject-1';
const ALFA = '10000000-0000-4000-8000-000000000001';
const BETA = '10000000-0000-4000-8000-000000000002';
const MARS = '20000000-0000-4000-8000-000000000001';
const VENUS = '20000000-0000-4000-8000-000000000002';
const PROJECT_A = '30000000-0000-4000-8000-000000000001';

const OFFERS: WorkContextOffers = {
  departments: [],
  teams: [
    { id: MARS, name: 'MARS', isMember: true, departmentIds: [] },
    { id: VENUS, name: 'VENUS', isMember: false, departmentIds: [] },
  ],
  projects: [{ id: PROJECT_A, name: 'A', key: 'A', departmentId: null, teamIds: [MARS, VENUS] }],
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
