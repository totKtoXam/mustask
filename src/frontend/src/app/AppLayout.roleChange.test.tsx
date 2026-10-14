import { describe, expect, it, vi } from 'vitest';
import { Route } from 'react-router-dom';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { organizationsKeys } from '../features/organizations/api/queryKeys';
import { useNoticeHost } from '../shared/lib/noticeCorner';
import type { WorkContextSource } from '../shared/lib/workContext';
import { servedFor, type TestRole } from '../test/organizationRoles';
import {
  authValue,
  LANGUAGES_READ,
  renderShell,
  stubRoutes,
  type ShellOptions,
} from '../test/renderShell';

// mod-016 design § 2.4 (DC-7, DC-11): a role change noticed on a re-read is
// announced once by the shell, in the corner it shares with the switcher's
// fallback notice and the pages' own notices.

const ORG = 'a0000000-0000-4000-8000-0000000000a1';
const OTHER = 'a0000000-0000-4000-8000-0000000000a2';

function source(overrides: Partial<WorkContextSource> = {}): WorkContextSource {
  return {
    status: 'ready',
    organizations: [
      { id: ORG, name: 'Альфа', projects: [] },
      { id: OTHER, name: 'Бета', projects: [] },
    ],
    selection: { organizationId: ORG },
    isReal: true,
    select: vi.fn(),
    ...overrides,
  };
}

function answer(role: TestRole) {
  const served = servedFor(role);
  return { kind: 'member' as const, role, permissions: served.permissions };
}

/** A page that shows a notice of its own, as the pages' hosts do. */
function NoticePage() {
  const claim = useNoticeHost(() => {});
  return (
    <button type="button" onClick={claim}>
      Показать уведомление страницы
    </button>
  );
}

const routes = (
  <>
    {stubRoutes()}
    <Route path="/notice" element={<NoticePage />} />
  </>
);

/**
 * The shell on a real selection. It waits until the access read the shell
 * makes on mount has answered: that is the *first* read of the organization,
 * which the role memory takes in silently.
 */
async function openShell(
  options: Partial<ShellOptions> & { contextSource?: WorkContextSource } = {},
) {
  const shell = renderShell({
    path: '/home',
    auth: authValue([LANGUAGES_READ]),
    contextSource: source(),
    routes,
    ...options,
  });
  await waitFor(() =>
    expect(shell.queryClient.isFetching({ queryKey: organizationsKeys.access(ORG) })).toBe(0),
  );
  return shell;
}

function change(
  shell: Awaited<ReturnType<typeof openShell>>,
  role: TestRole,
  organizationId = ORG,
) {
  act(() => {
    shell.queryClient.setQueryData(organizationsKeys.access(organizationId), answer(role));
  });
}

const NOTICE = /Ваша роль в организации/;

describe('mod-016 design § 2.4 Scenario: a role change noticed on a re-read', () => {
  it('says the organization and the new role, once, in an info alert with a way to close it', async () => {
    const shell = await openShell();

    change(shell, 'viewer');

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Ваша роль в организации «Альфа» изменилась: «Читатель».');
    expect(within(alert).getByRole('button')).toBeInTheDocument();
    expect(screen.getAllByText(NOTICE)).toHaveLength(1);
  });

  it('is not raised for the first read of the organization, which the shell makes itself', async () => {
    await openShell({ access: 'unread' });

    expect(screen.queryByText(NOTICE)).not.toBeInTheDocument();
  });

  it('is not raised when the re-read shows the same role', async () => {
    const shell = await openShell();

    change(shell, 'admin');

    expect(screen.queryByText(NOTICE)).not.toBeInTheDocument();
  });

  it('names the organization whose role changed', async () => {
    const shell = await openShell();
    change(shell, 'member', OTHER);

    change(shell, 'viewer', OTHER);

    expect(
      await screen.findByText(/в организации «Бета» изменилась: «Читатель»/),
    ).toBeInTheDocument();
  });

  it('waits for the organization’s name when the switcher’s list does not hold it yet', async () => {
    const shell = await openShell({ contextSource: source({ organizations: [] }) });

    change(shell, 'viewer');
    expect(screen.queryByText(NOTICE)).not.toBeInTheDocument();

    shell.update({ contextSource: source() });

    expect(await screen.findByText(/в организации «Альфа» изменилась/)).toBeInTheDocument();
  });

  it('yields to the switcher’s fallback notice, which wins', async () => {
    const shell = await openShell({
      contextSource: source({ fallback: { id: 1, names: ['Бета'] } }),
    });

    change(shell, 'viewer');

    expect(await screen.findByText(/Ранее выбранный контекст недоступен/)).toBeInTheDocument();
    expect(screen.queryByText(NOTICE)).not.toBeInTheDocument();
  });

  it('closes for a newer notice of a page, which claims the corner', async () => {
    const user = userEvent.setup();
    const shell = await openShell({ path: '/notice' });
    change(shell, 'viewer');
    expect(await screen.findByText(NOTICE)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Показать уведомление страницы' }));

    expect(screen.queryByText(NOTICE)).not.toBeInTheDocument();
  });

  it('closes with its own button', async () => {
    const user = userEvent.setup();
    const shell = await openShell();
    change(shell, 'viewer');
    const alert = await screen.findByRole('alert');

    await user.click(within(alert).getByRole('button'));

    expect(screen.queryByText(NOTICE)).not.toBeInTheDocument();
  });
});
