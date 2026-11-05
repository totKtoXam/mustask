/* eslint-disable react-refresh/only-export-components -- a test-only module: the frame and the access wrapper live beside the helper that mounts them. */
// Helpers of mod-005's card tests: the card opened as a role meets it — the served codes decide
// «Удалить», «Восстановить» and «Удалить навсегда», as the hosts pass them — over the stories'
// dataset, with the API's refusals one call away.
import { act, useEffect, useState, type ReactNode } from 'react';
import { expect, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../../test/mocks/server';
import { renderWithProviders } from '../../../../test/renderWithProviders';
import { checkFor, type TestRole } from '../../../../test/organizationRoles';
import { AccessCheckContext } from '../../../../shared/lib/access';
import { workItemsPermissions } from '../../permissions';
import { serveWorkItems, type WorkItemsTestApi } from '../../testApi';
import { PROBLEM_HEADERS, hierarchyDataset, problem, type Dataset } from '../../testFixtures';
import type { CardFrameProps } from './cardFrame';
import { PageFrame } from './KitFrames';
import { CardHost, type CardOptions } from './testCard';

function pageFrame(frame: CardFrameProps) {
  return <PageFrame frame={frame} />;
}

/** Hands the test the means to change the served role under a card that is open (a lost code). */
let changeRole: (role: TestRole) => void = () => {};

function ServedAccess({ initial, children }: { initial: TestRole; children: ReactNode }) {
  const [role, setRole] = useState(initial);
  useEffect(() => {
    changeRole = setRole;
  }, []);
  return (
    <AccessCheckContext.Provider value={checkFor(role)}>{children}</AccessCheckContext.Provider>
  );
}

/**
 * Opens the card of `identifier` as `role` does: read-only without the write code, with the
 * delete and purge codes the role holds, and the dialogs asking the same served access. The
 * stored data is prepared before the card reads it.
 */
export async function openAs(
  identifier: string,
  options: {
    role?: TestRole;
    data?: Dataset;
    prepare?: (data: Dataset, api: WorkItemsTestApi) => void;
    card?: CardOptions;
  } = {},
) {
  const data = options.data ?? hierarchyDataset();
  const api = serveWorkItems(data);
  options.prepare?.(data, api);
  const role = options.role ?? 'admin';
  const access = checkFor(role);
  const notify = vi.fn();
  const onGone = vi.fn();
  const onChanged = vi.fn();
  const onPurged = vi.fn();
  const view = renderWithProviders(
    <CardHost
      identifier={identifier}
      card={{
        mode: 'page',
        notify,
        onGone,
        // The dialog's host hears every accepted write and read with one callback (BR-C-21).
        onSaved: onChanged,
        onRefreshed: onChanged,
        onChanged,
        onPurged,
        renderFrame: pageFrame,
        readOnly: !access(workItemsPermissions.workItemWrite, 'organization'),
        canDelete: access(workItemsPermissions.workItemDelete, 'organization'),
        canPurge: access(workItemsPermissions.workItemPurge, 'organization'),
        ...options.card,
      }}
    />,
    {
      wrapper: (children) => <ServedAccess initial={role}>{children}</ServedAccess>,
    },
  );
  await screen.findByRole('textbox', { name: 'Заголовок' });
  return {
    api,
    data,
    view,
    notify,
    onGone,
    onChanged,
    onPurged,
    /** The served role changes under the open card: a code is gone, or comes. */
    serve: (next: TestRole) => act(() => changeRole(next)),
  };
}

/** «Ещё действия». */
export const moreActions = () => screen.getByRole('button', { name: 'Ещё действия' });

/** The names of the entries «⋮» offers, in order, with how many rules stand between them. */
export async function menuEntries() {
  await userEvent.click(moreActions());
  const menu = await screen.findByRole('menu', { name: 'Ещё действия' });
  const names = within(menu)
    .getAllByRole('menuitem')
    .map((entry) => entry.textContent ?? '');
  return { menu, names, rules: within(menu).queryAllByRole('separator').length };
}

/** Opens «⋮» and chooses `name`. */
export async function chooseFromMenu(name: string) {
  await userEvent.click(moreActions());
  await userEvent.click(await screen.findByRole('menuitem', { name }));
}

/** The dialog titled `title`. */
export const dialogTitled = (title: string) => screen.findByRole('dialog', { name: title });

/** Waits until no dialog is drawn. */
export const noDialog = () =>
  waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

/** The POSTs the API received to `…/delete`, `…/restore` or `…/purge`. */
export function postsTo(api: WorkItemsTestApi, route: 'delete' | 'restore' | 'purge') {
  return api
    .writes()
    .filter((request) => request.method === 'POST' && request.path.endsWith(`/${route}`));
}

/** Answers every call to `url` with the catalog's `code`, in its own status. */
export function refuseWith(url: string, code: string): void {
  const body = problem(code);
  server.use(
    http.post(url, () =>
      HttpResponse.json(body, { status: body.status, headers: PROBLEM_HEADERS }),
    ),
  );
}

/** Holds the answer to a request of `method` to `url` until `release()`; the real handler then answers. */
function hold(method: 'get' | 'post', url: string) {
  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  server.use(
    http[method](url, async () => {
      await held;
      // No answer of its own: MSW passes the request on to the next handler.
      return undefined;
    }),
  );
  return release;
}

export const holdPost = (url: string) => hold('post', url);
export const holdGet = (url: string) => hold('get', url);
