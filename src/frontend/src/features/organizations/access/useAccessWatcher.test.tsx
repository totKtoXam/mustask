import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { organizationsKeys } from '../api/queryKeys';
import type { OrganizationRole } from '../lib/roles';
import type { AccessAnswer } from './accessAnswer';
import { forgetAccess } from './accessCache';
import { lastRole, rememberRole } from './roleMemory';
import { serveAccess } from './testAccess';
import { useAccessWatcher, type AccessWatcherOptions } from './useAccessWatcher';
import { useOrganizationAccess } from './useOrganizationAccess';

const ORG = 'a0000000-0000-4000-8000-000000000001';
const OTHER = 'a0000000-0000-4000-8000-000000000002';

function member(role: OrganizationRole): AccessAnswer {
  return { kind: 'member', role, permissions: ['organizations.organization.read'] };
}

function setup(initial: Partial<AccessWatcherOptions> = {}) {
  const client = new QueryClient();
  const names: Record<string, string> = { [ORG]: 'Альфа' };
  const invalidate = vi.spyOn(client, 'invalidateQueries');
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  const rendered = renderHook((options: AccessWatcherOptions) => useAccessWatcher(options), {
    wrapper,
    initialProps: {
      fallbackShown: false,
      organizationName: (id: string) => names[id],
      ...initial,
    },
  });
  const answer = (organizationId: string, value: AccessAnswer) =>
    act(() => {
      client.setQueryData(organizationsKeys.access(organizationId), value);
    });
  const listReads = () =>
    invalidate.mock.calls.filter(
      ([filters]) =>
        JSON.stringify(filters?.queryKey) ===
        JSON.stringify(organizationsKeys.organizations('active')),
    ).length;
  return { client, names, rendered, answer, listReads };
}

describe('the role-change notice (mod-016 design § 2.4, plan P-11)', () => {
  it('is never raised for the first read of an organization', () => {
    const { rendered, answer } = setup();

    answer(ORG, member('admin'));

    expect(rendered.result.current.roleChange).toBeNull();
  });

  it('is raised when a re-read shows a different role, with the organization’s name and the role', () => {
    const { rendered, answer } = setup();
    answer(ORG, member('member'));

    answer(ORG, member('viewer'));

    expect(rendered.result.current.roleChange?.text).toBe(
      'Ваша роль в организации «Альфа» изменилась: «Читатель».',
    );
  });

  it('is not raised when the re-read shows the same role', () => {
    const { rendered, answer } = setup();
    answer(ORG, member('member'));

    answer(ORG, { kind: 'member', role: 'member', permissions: ['workitems.workitem.read'] });

    expect(rendered.result.current.roleChange).toBeNull();
  });

  it('is not raised for the user’s own change, which remembers the new role before the re-read', () => {
    const { client, rendered, answer } = setup();
    answer(ORG, member('admin'));

    rememberRole(client, ORG, 'member');
    answer(ORG, member('member'));

    expect(rendered.result.current.roleChange).toBeNull();
  });

  it('keeps the roles of two organizations apart', () => {
    const { rendered, answer } = setup();
    answer(ORG, member('admin'));

    answer(OTHER, member('viewer'));

    expect(rendered.result.current.roleChange).toBeNull();
  });

  it('waits for the organization’s name when the switcher’s list does not hold it yet', () => {
    const { names, rendered, answer } = setup();
    delete names[ORG];
    answer(ORG, member('member'));
    answer(ORG, member('viewer'));
    expect(rendered.result.current.roleChange).toBeNull();

    names[ORG] = 'Альфа';
    rendered.rerender({
      fallbackShown: false,
      organizationName: (id: string) => names[id],
    });

    expect(rendered.result.current.roleChange?.text).toContain('«Альфа»');
  });

  it('is dropped when the access turns «not a member» while it waits', () => {
    const { names, rendered, answer } = setup();
    delete names[ORG];
    answer(ORG, member('member'));
    answer(ORG, member('viewer'));

    answer(ORG, { kind: 'not-member' });
    names[ORG] = 'Альфа';
    rendered.rerender({ fallbackShown: false, organizationName: (id: string) => names[id] });

    expect(rendered.result.current.roleChange).toBeNull();
  });

  it('is not raised while the switcher’s fallback notice is shown, and a waiting one is dropped when it appears', () => {
    const shown = setup({ fallbackShown: true });
    shown.answer(ORG, member('member'));
    shown.answer(ORG, member('viewer'));
    expect(shown.rendered.result.current.roleChange).toBeNull();

    const waiting = setup();
    waiting.answer(ORG, member('member'));
    waiting.answer(ORG, member('viewer'));
    expect(waiting.rendered.result.current.roleChange).not.toBeNull();
    waiting.rendered.rerender({
      fallbackShown: true,
      organizationName: (id: string) => waiting.names[id],
    });
    waiting.rendered.rerender({
      fallbackShown: false,
      organizationName: (id: string) => waiting.names[id],
    });

    expect(waiting.rendered.result.current.roleChange).toBeNull();
  });

  it('is replaced by a newer change, and closes through dismissRoleChange', () => {
    const { rendered, answer } = setup();
    answer(ORG, member('member'));
    answer(ORG, member('viewer'));
    const first = rendered.result.current.roleChange;

    answer(ORG, member('admin'));
    const second = rendered.result.current.roleChange;
    expect(second?.id).not.toBe(first?.id);
    expect(second?.text).toContain('«Администратор»');

    act(() => rendered.result.current.dismissRoleChange());
    expect(rendered.result.current.roleChange).toBeNull();
  });

  it('forgets the role of an organization whose access leaves the cache: the next read is a first one', () => {
    const { client, rendered, answer } = setup();
    answer(ORG, member('admin'));

    act(() => {
      client.removeQueries({ queryKey: organizationsKeys.access(ORG) });
    });
    answer(ORG, member('viewer'));

    expect(rendered.result.current.roleChange).toBeNull();
  });

  it('forgets it when the caller has left the organization (forgetAccess), and only then', () => {
    const { client, names, rendered, answer } = setup();
    names[OTHER] = 'Бета';
    answer(ORG, member('admin'));
    answer(OTHER, member('admin'));

    act(() => forgetAccess(client, ORG));
    expect(lastRole(client, ORG)).toBeUndefined();
    expect(lastRole(client, OTHER)).toBe('admin');
    answer(ORG, member('viewer'));
    expect(rendered.result.current.roleChange).toBeNull();
    answer(OTHER, member('viewer'));

    expect(rendered.result.current.roleChange?.text).toBe(
      'Ваша роль в организации «Бета» изменилась: «Читатель».',
    );
  });
});

// Design § 6.1: the codes read earlier in this tab show from the first frame on a
// switch back; § 2.4: a role changed meanwhile raises the notice on the re-read of
// that switch back. Both need the access entry to outlive the time the cache keeps
// an unused entry (five minutes by default): it lives for the tab, and leaves it
// only through `forgetAccess`. The test client keeps an unused entry for 30 ms.
describe('an organization’s access entry lives as long as the tab (mod-016 design § 6.1, § 2.4)', () => {
  function useBoth(organizationId: string, options: AccessWatcherOptions) {
    useOrganizationAccess(organizationId);
    return useAccessWatcher(options);
  }

  it('stays, with the role last read, after nothing has observed it for longer than an unused entry is kept; a switch back with a changed role raises the notice', async () => {
    let role: 'member' | 'viewer' = 'member';
    serveAccess((organizationId) => (organizationId === ORG ? role : 'admin'));
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, gcTime: 30 } },
    });
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    const options: AccessWatcherOptions = {
      fallbackShown: false,
      organizationName: (id) => (id === ORG ? 'Альфа' : undefined),
    };
    const rendered = renderHook(
      ({ organizationId }: { organizationId: string }) => useBoth(organizationId, options),
      { wrapper, initialProps: { organizationId: ORG } },
    );
    await waitFor(() =>
      expect(client.getQueryData(organizationsKeys.access(ORG))).toMatchObject({ role: 'member' }),
    );

    // The switcher moves to the other organization: nothing observes the first one.
    rendered.rerender({ organizationId: OTHER });
    await waitFor(() =>
      expect(client.getQueryData(organizationsKeys.access(OTHER))).toMatchObject({ role: 'admin' }),
    );
    await new Promise((resolve) => setTimeout(resolve, 150));

    expect(client.getQueryData(organizationsKeys.access(ORG))).toMatchObject({ role: 'member' });
    expect(lastRole(client, ORG)).toBe('member');

    // A switch back: the codes are there at once, the re-read finds a role changed meanwhile.
    role = 'viewer';
    rendered.rerender({ organizationId: ORG });
    expect(client.getQueryData(organizationsKeys.access(ORG))).toMatchObject({ role: 'member' });
    await waitFor(() =>
      expect(rendered.result.current.roleChange?.text).toBe(
        'Ваша роль в организации «Альфа» изменилась: «Читатель».',
      ),
    );
  });
});

describe('a turn to «not a member» (mod-016 plan P-15)', () => {
  it('reads the organization list once, and a second 404 for the same organization is not a turn', () => {
    const { rendered, answer, listReads } = setup();
    answer(ORG, member('admin'));
    expect(listReads()).toBe(0);

    answer(ORG, { kind: 'not-member' });
    expect(listReads()).toBe(1);

    answer(ORG, { kind: 'not-member' });
    expect(listReads()).toBe(1);
    expect(rendered.result.current.roleChange).toBeNull();
  });

  it('counts a first answer of 404 as a turn, and a return to codes and another 404 as another', () => {
    const { answer, listReads } = setup();

    answer(ORG, { kind: 'not-member' });
    expect(listReads()).toBe(1);

    answer(ORG, member('viewer'));
    answer(ORG, { kind: 'not-member' });
    expect(listReads()).toBe(2);
  });

  it('reads the list for the organization that turned, not for the one that did not', () => {
    const { answer, listReads } = setup();
    answer(ORG, member('admin'));
    answer(OTHER, member('admin'));

    answer(OTHER, { kind: 'not-member' });

    expect(listReads()).toBe(1);
  });
});
