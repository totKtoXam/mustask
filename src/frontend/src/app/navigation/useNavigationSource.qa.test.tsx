import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { WorkContextOffers } from '../../shared/lib/workContext';
import { readRemembered, writeRemembered } from './selection';
import { NO_SELECTION } from './selectorFit';
import { useNavigationSource } from './useNavigationSource';

// feat-043 QA, US-1 and US-8: an address that names an object which is gone (BR-NV-7, BR-NV-10).
// The slice's reads are stubbed as in useNavigationSource.test.tsx; the source is the real one.

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
const MARS = '20000000-0000-4000-8000-000000000001';
const PROJECT_A = '30000000-0000-4000-8000-000000000001';
const GONE_PROJECT = '30000000-0000-4000-8000-0000000000ff';

const OFFERS: WorkContextOffers = {
  departments: [],
  teams: [{ id: MARS, name: 'MARS', isMember: true, departmentIds: [] }],
  projects: [{ id: PROJECT_A, name: 'A', key: 'A', departmentId: null, teamIds: [MARS] }],
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

  // QA defect F-1 (qa.md): the address's selection overwrites the remembered one even when the
  // object it names does not exist, so the context changes. `it.fails` passes while the defect
  // stands; when it is fixed this test turns red: drop `.fails`.
  it.fails(
    'names another object than the remembered one: the context does not change, no notice',
    async () => {
      writeRemembered(window.localStorage, USER, ALFA, { ...NO_SELECTION, team: MARS });

      const { result } = render(`/organizations/${ALFA}/projects/${GONE_PROJECT}/general`);

      // Let the source settle: the offers are ready at once, so one tick is enough.
      await waitFor(() => expect(result.current.selection.organizationId).toBe(ALFA));
      expect(result.current.fallback).toBeNull();
      expect(readRemembered(window.localStorage, USER, ALFA)).toMatchObject({ team: MARS });
    },
  );
});
