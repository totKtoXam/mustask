import { afterEach, describe, expect, it } from 'vitest';
import { Link } from 'react-router-dom';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { recordAppearances } from '../../../test/appearances';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { servedFor, withoutCodes } from '../../../test/organizationRoles';
import { API, PROBLEM_HEADERS, problem } from '../testFixtures';
import { organizationsKeys } from '../api/queryKeys';
import {
  ORG_A,
  ORG_B,
  READ_CODE,
  WRITE_CODE,
  forbiddenResponse,
  notFoundResponse,
  pageAt,
  renderGate,
  serveAccess,
  serveOrganizationList,
  StubCaller,
  StubPage,
} from './testAccess';
import { QueryClient, focusManager } from '@tanstack/react-query';

afterEach(() => {
  focusManager.setFocused(undefined);
});

/** The switcher's own first read of the list, plus the one read a turn to "not a member" starts (P-15). */
const LIST_READS_AFTER_ONE_TURN = 2;

function skeleton() {
  return screen.queryByRole('status', { name: 'Загрузка данных' });
}

describe('the gate reads the organization of the address (mod-016 plan P-3)', () => {
  it('sees the :organizationId of the child route from its pathless layout', async () => {
    const access = serveAccess(() => 'admin');

    renderGate({ path: pageAt(ORG_A) });

    expect(await screen.findByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();
    expect(access.requests).toEqual([ORG_A]);
  });
});

describe('the five rows of an organization page (mod-016 design § 2.1)', () => {
  it('row 1: the header and the skeleton while nothing is held and the first read runs, never «Доступ ограничен»', async () => {
    const open = gate();
    serveAccess(async () => {
      await open.opened;
      return HttpResponse.json(servedFor('admin'));
    });

    renderGate({ path: pageAt(ORG_A) });

    expect(await screen.findByRole('heading', { level: 1, name: 'Вещи' })).toBeInTheDocument();
    const status = screen.getByRole('status', { name: 'Загрузка данных' });
    expect(within(status).getByText('Название')).toBeInTheDocument();
    expect(screen.queryByText('Доступ ограничен')).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Страница' })).not.toBeInTheDocument();

    open.open();
    expect(await screen.findByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();
    expect(skeleton()).not.toBeInTheDocument();
  });

  it('row 2: the banner «Не удалось загрузить права доступа» with «Повторить», which is row 1 while it runs', async () => {
    const user = userEvent.setup();
    const retryOpen = gate();
    let calls = 0;
    serveAccess(async () => {
      calls += 1;
      if (calls === 1) {
        return HttpResponse.json(problem('organizations.access.forbidden'), {
          status: 500,
          headers: PROBLEM_HEADERS,
        });
      }
      await retryOpen.opened;
      return HttpResponse.json(servedFor('admin'));
    });

    renderGate({ path: pageAt(ORG_A) });

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Не удалось загрузить права доступа')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Вещи' })).toBeInTheDocument();
    expect(screen.queryByText('Доступ ограничен')).not.toBeInTheDocument();

    await user.click(within(alert).getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    retryOpen.open();
    expect(await screen.findByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();
  });

  it('row 3: «Организация не найдена», full page, when the access read answers 404', async () => {
    serveAccess(() => notFoundResponse());

    renderGate({ path: pageAt(ORG_A) });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Вещи' })).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('row 4: «Доступ ограничен» when the codes are held and the page’s is missing', async () => {
    serveAccess(() => HttpResponse.json(withoutCodes('viewer', READ_CODE)));

    renderGate({ path: pageAt(ORG_A) });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Доступ ограничен' }),
    ).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Страница' })).not.toBeInTheDocument();
  });

  it('row 5: the page, with a control for a writer and none for a reader', async () => {
    serveAccess(() => 'admin');
    const writer = renderGate({ path: pageAt(ORG_A) });
    expect(await screen.findByRole('button', { name: 'Создать' })).toBeInTheDocument();
    writer.unmount();

    serveAccess(() => 'viewer');
    renderGate({ path: pageAt(ORG_A) });
    expect(await screen.findByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Создать' })).not.toBeInTheDocument();
  });

  it('draws exactly one row for every state the tab can hold', async () => {
    serveAccess(() => 'admin');
    const { queryClient } = renderGate({ path: pageAt(ORG_A) });
    await screen.findByRole('heading', { level: 1, name: 'Страница' });
    const rows = () =>
      [
        skeleton() && 'loading',
        screen.queryByText('Не удалось загрузить права доступа') && 'failed',
        screen.queryByRole('heading', { name: 'Организация не найдена' }) && 'notFound',
        screen.queryByRole('heading', { name: 'Доступ ограничен' }) && 'denied',
        screen.queryByRole('heading', { name: 'Страница' }) && 'content',
      ].filter(Boolean);

    const key = organizationsKeys.access(ORG_A);
    expect(rows()).toEqual(['content']);

    act(() => queryClient.setQueryData(key, { kind: 'not-member' }));
    await waitFor(() => expect(rows()).toEqual(['notFound']));

    act(() =>
      queryClient.setQueryData(key, {
        kind: 'member',
        role: 'viewer',
        permissions: ['organizations.team.read'],
      }),
    );
    await waitFor(() => expect(rows()).toEqual(['denied']));

    act(() =>
      queryClient.setQueryData(key, {
        kind: 'member',
        role: 'viewer',
        permissions: [READ_CODE],
      }),
    );
    await waitFor(() => expect(rows()).toEqual(['content']));
  });
});

describe('re-reads are background reads (mod-016 design § 2.2)', () => {
  it('never bring the skeleton back over held codes, and a failed one changes nothing', async () => {
    const open = gate();
    let calls = 0;
    serveAccess(async () => {
      calls += 1;
      if (calls === 1) {
        return HttpResponse.json(servedFor('admin'));
      }
      await open.opened;
      return HttpResponse.json({ status: 500 }, { status: 500 });
    });
    const { queryClient } = renderGate({ path: pageAt(ORG_A) });
    await screen.findByRole('heading', { level: 1, name: 'Страница' });

    act(() => {
      void queryClient.invalidateQueries({ queryKey: organizationsKeys.access(ORG_A) });
    });
    await waitFor(() => expect(calls).toBe(2));

    expect(skeleton()).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();

    open.open();
    await waitFor(() =>
      expect(queryClient.isFetching({ queryKey: organizationsKeys.access(ORG_A) })).toBe(0),
    );
    expect(screen.getByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();
    expect(screen.queryByText('Не удалось загрузить права доступа')).not.toBeInTheDocument();
  });

  it('hold row 3 while a re-read runs, after one that failed and after one that answers 404 again; a 200 replaces it', async () => {
    const results: ('404' | '500' | 'held' | 'ok')[] = ['404', 'held', '500', '404', 'ok'];
    const open = gate();
    let calls = 0;
    serveAccess(async () => {
      const result = results[calls] ?? 'ok';
      calls += 1;
      if (result === '404') return notFoundResponse();
      if (result === '500') return HttpResponse.json({ status: 500 }, { status: 500 });
      if (result === 'held') {
        await open.opened;
        return notFoundResponse();
      }
      return HttpResponse.json(servedFor('admin'));
    });
    const { queryClient } = renderGate({ path: pageAt(ORG_A) });
    const key = organizationsKeys.access(ORG_A);
    const frame = () => screen.getByRole('heading', { level: 1, name: 'Организация не найдена' });
    const refetch = () =>
      act(async () => {
        await queryClient.invalidateQueries({ queryKey: key }).catch(() => undefined);
      });
    await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' });

    // A re-read running.
    act(() => {
      void queryClient.invalidateQueries({ queryKey: key });
    });
    await waitFor(() => expect(calls).toBe(2));
    expect(frame()).toBeInTheDocument();
    expect(skeleton()).not.toBeInTheDocument();
    open.open();
    await waitFor(() => expect(queryClient.isFetching({ queryKey: key })).toBe(0));
    expect(frame()).toBeInTheDocument();

    // A re-read that failed.
    await refetch();
    expect(calls).toBe(3);
    expect(frame()).toBeInTheDocument();
    expect(screen.queryByText('Не удалось загрузить права доступа')).not.toBeInTheDocument();

    // A re-read that answers 404 again.
    await refetch();
    expect(calls).toBe(4);
    expect(frame()).toBeInTheDocument();

    // A re-read that answers 200 takes the frame away.
    await refetch();
    expect(await screen.findByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();
    expect(
      screen.queryByRole('heading', { name: 'Организация не найдена' }),
    ).not.toBeInTheDocument();
  });

  it('is triggered by a refocus of the window, in row 3 as in row 5', async () => {
    let calls = 0;
    serveAccess(() => {
      calls += 1;
      return calls === 1 ? notFoundResponse() : HttpResponse.json(servedFor('member'));
    });
    renderGate({ path: pageAt(ORG_A) });
    await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' });

    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    expect(await screen.findByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();
    expect(calls).toBe(2);
  });
});

describe('the bound check and the organization of the address (mod-016 design § 2.6)', () => {
  /** What the tab holds for an organization whose access was read earlier. */
  function heldAs(queryClient: QueryClient, organizationId: string, role: 'admin' | 'viewer') {
    const served = servedFor(role);
    queryClient.setQueryData(organizationsKeys.access(organizationId), {
      kind: 'member',
      role: served.role,
      permissions: served.permissions,
    });
  }

  function clientHolding(organizationId: string, role: 'admin' | 'viewer') {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    heldAs(queryClient, organizationId, role);
    return queryClient;
  }

  it('decides a page of B by B’s codes, whatever A’s are', async () => {
    serveAccess((organizationId) => (organizationId === ORG_A ? 'admin' : 'viewer'));
    const { queryClient } = renderGate({ path: pageAt(ORG_B) });
    // A's codes are held in the tab: an administrator's.
    heldAs(queryClient, ORG_A, 'admin');

    expect(await screen.findByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Создать' })).not.toBeInTheDocument();
    expect(servedFor('admin').permissions).toContain(WRITE_CODE);
  });

  // Design § 12, "whose codes": a link into B while the remembered selection is
  // A — an administrator in A, a reader in B. The selection follows the address
  // only once the address's lists are read, and never when they fail, so the
  // page must not borrow the selection's codes at any frame (US-4 "at no
  // moment"). The selection names A, and the tab holds A's administrator codes.
  it('whose codes: the selection is A (administrator), the address is B (reader): no write control on B’s page before B’s codes are read, nor after', async () => {
    const open = gate();
    const access = serveAccess(async (organizationId) => {
      if (organizationId === ORG_A) {
        return HttpResponse.json(servedFor('admin'));
      }
      await open.opened;
      return HttpResponse.json(servedFor('viewer'));
    });
    const create = recordAppearances('Создать');

    renderGate({
      path: pageAt(ORG_B),
      selection: { organizationId: ORG_A },
      queryClient: clientHolding(ORG_A, 'admin'),
    });

    // B's read is running: row 1, the skeleton, with A's codes in the tab.
    await waitFor(() => expect(access.requests).toContain(ORG_B));
    expect(skeleton()).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Создать' })).not.toBeInTheDocument();

    // B's codes arrive: the reader's page.
    open.open();
    expect(await screen.findByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Создать' })).not.toBeInTheDocument();
    expect(create.count()).toBe(0);
    create.stop();
  });

  it('whose codes: a switch from A to B shows none of A’s controls on B’s page at any frame', async () => {
    const user = userEvent.setup();
    const open = gate();
    serveAccess(async (organizationId) => {
      if (organizationId === ORG_A) {
        return HttpResponse.json(servedFor('admin'));
      }
      await open.opened;
      return HttpResponse.json(servedFor('viewer'));
    });
    renderGate({
      path: pageAt(ORG_A),
      selection: { organizationId: ORG_A },
      page: (
        <StubPage>
          <Link to={pageAt(ORG_B)}>Открыть Б</Link>
        </StubPage>
      ),
    });
    await screen.findByRole('button', { name: 'Создать' });
    const create = recordAppearances('Создать');

    await user.click(screen.getByRole('link', { name: 'Открыть Б' }));

    // B's codes unread: the skeleton, and A's administrator codes still held.
    expect(await screen.findByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Создать' })).not.toBeInTheDocument();
    open.open();
    expect(await screen.findByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Создать' })).not.toBeInTheDocument();
    expect(create.count()).toBe(0);
    create.stop();
  });
});

describe('a 404 «Организация не найдена» from a scoped call (mod-016 design § 2.2, DC-12)', () => {
  it('turns the page into row 3 at once, reads the organization list once and the access once more, and that 404 starts neither again', async () => {
    const user = userEvent.setup();
    const reread = gate();
    const list = serveOrganizationList([]);
    const access = serveAccess(async () => {
      if (access.requests.length > 1) {
        await reread.opened;
        return notFoundResponse();
      }
      return HttpResponse.json(servedFor('admin'));
    });
    server.use(http.post(`${API}/api/organizations/${ORG_A}/projects`, () => notFoundResponse()));
    renderGate({ path: pageAt(ORG_A), page: <StubCaller organizationId={ORG_A} /> });
    await user.click(await screen.findByRole('button', { name: 'Вызвать' }));

    // At once: before the re-read answers, no skeleton, no banner, no alert.
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    expect(skeleton()).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Вызвать' })).not.toBeInTheDocument();
    await waitFor(() => expect(access.requests).toHaveLength(2));
    await waitFor(() => expect(list.count()).toBe(LIST_READS_AFTER_ONE_TURN));

    // The frame holds while the re-read runs.
    expect(
      screen.getByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
    expect(skeleton()).not.toBeInTheDocument();
    reread.open();
    await waitFor(() => expect(access.requests).toHaveLength(2));
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(access.requests).toHaveLength(2);
    expect(list.count()).toBe(LIST_READS_AFTER_ONE_TURN);
    expect(
      screen.getByRole('heading', { level: 1, name: 'Организация не найдена' }),
    ).toBeInTheDocument();
  });

  it('reads the organization list once also when the access read alone answers 404', async () => {
    const list = serveOrganizationList([]);
    serveAccess(() => notFoundResponse());

    renderGate({ path: pageAt(ORG_A) });

    await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' });
    await waitFor(() => expect(list.count()).toBe(LIST_READS_AFTER_ONE_TURN));
  });

  it('is not taken for a 404 of a project or a team inside a found organization', async () => {
    const user = userEvent.setup();
    const access = serveAccess(() => 'admin');
    server.use(
      http.post(`${API}/api/organizations/${ORG_A}/projects`, () =>
        HttpResponse.json(problem('organizations.project.not_found'), {
          status: 404,
          headers: PROBLEM_HEADERS,
        }),
      ),
    );
    renderGate({ path: pageAt(ORG_A), page: <StubCaller organizationId={ORG_A} /> });
    await user.click(await screen.findByRole('button', { name: 'Вызвать' }));

    // The page stays; the codes are read again, once.
    await waitFor(() => expect(access.requests).toHaveLength(2));
    expect(screen.getByRole('button', { name: 'Вызвать' })).toBeInTheDocument();
    expect(await screen.findByRole('alert')).toHaveTextContent('Проект не найден');
  });
});

describe('a 403 re-reads the codes (mod-016 design § 2.3)', () => {
  it('takes away a control whose code is gone with no reload, keeps the refusal and says why the role changed', async () => {
    const user = userEvent.setup();
    let role: 'admin' | 'viewer' = 'admin';
    serveAccess(() => role);
    server.use(http.post(`${API}/api/organizations/${ORG_A}/projects`, () => forbiddenResponse()));
    renderGate({
      path: pageAt(ORG_A),
      page: <StubCaller organizationId={ORG_A} />,
      names: { [ORG_A]: 'Альфа' },
    });
    await screen.findByRole('button', { name: 'Создать' });
    role = 'viewer';

    // The refused call re-reads the codes; «Создать» goes and the shell says why.
    await user.click(screen.getByRole('button', { name: 'Вызвать' }));
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: 'Создать' })).not.toBeInTheDocument(),
    );

    expect(screen.getByRole('heading', { level: 1, name: 'Страница' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Недостаточно прав');
    expect(await screen.findByTestId('role-change')).toHaveTextContent(
      'Ваша роль в организации «Альфа» изменилась: «Читатель».',
    );
  });
});

describe('focus rescue (mod-016 plan P-10)', () => {
  it('hands focus to the main region when the focused control goes with the codes, never to body', async () => {
    let role: 'admin' | 'viewer' = 'admin';
    serveAccess(() => role);
    const { queryClient } = renderGate({ path: pageAt(ORG_A), watcher: false });
    const create = await screen.findByRole('button', { name: 'Создать' });
    create.focus();
    expect(create).toHaveFocus();
    role = 'viewer';

    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: organizationsKeys.access(ORG_A) });
    });

    await waitFor(() =>
      expect(screen.queryByRole('button', { name: 'Создать' })).not.toBeInTheDocument(),
    );
    expect(document.getElementById('main-content')).toHaveFocus();
  });

  it('leaves focus alone when nothing that had it is gone', async () => {
    serveAccess(() => 'admin');
    const { queryClient } = renderGate({ path: pageAt(ORG_A), watcher: false });
    const create = await screen.findByRole('button', { name: 'Создать' });
    create.focus();

    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: organizationsKeys.access(ORG_A) });
    });

    expect(create).toHaveFocus();
  });

  it('moves focus to the main region when a page turns into «Организация не найдена»', async () => {
    serveAccess(() => 'admin');
    const { queryClient } = renderGate({ path: pageAt(ORG_A), watcher: false });
    const create = await screen.findByRole('button', { name: 'Создать' });
    create.focus();

    act(() => queryClient.setQueryData(organizationsKeys.access(ORG_A), { kind: 'not-member' }));

    await screen.findByRole('heading', { level: 1, name: 'Организация не найдена' });
    expect(document.getElementById('main-content')).toHaveFocus();
  });
});
