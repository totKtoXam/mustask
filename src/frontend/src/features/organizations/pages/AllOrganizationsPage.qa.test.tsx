import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { ADMIN_ORGANIZATIONS_PATH } from '../paths';
import { organizationsPermissions } from '../permissions';
import { ORGANIZATIONS_URL, ids } from '../testFixtures';
import { membersTable, openPage } from '../testMembersPage';
import { renderPage } from '../testRender';
import {
  ROOT_ADMIN,
  VERA,
  problemResponse,
  rescueOrganizations,
  serveRescue,
  type RescueOptions,
} from '../testRescue';

// feat-033 frontend QA pass 1 (qa.md, Frontend): the gaps the developer's tests leave — the
// reason trimmed as the server trims it (backend D-1), the page states while a page loads or
// vanishes, the spaces-only filter, focus after a refusal and in the history dialog, the
// «Участники» host's focus return, and no list read after the page turned «Доступ ограничен».

const RESCUE = [organizationsPermissions.organizationRescue];
const ORGANIZATION = 'Организация 001';
/** U+FEFF, the byte-order mark: written by its code so no invisible character sits in the source. */
const BOM = String.fromCharCode(0xfeff);

function open(options: RescueOptions = {}) {
  const api = serveRescue(options);
  renderPage({ path: ADMIN_ORGANIZATIONS_PATH, permissions: RESCUE });
  return api;
}

function table() {
  return screen.findByRole('table', { name: 'Все организации' });
}

function listReads(api: ReturnType<typeof serveRescue>) {
  return api.requests.filter(
    (request) => request.method === 'GET' && request.path.endsWith('/api/organizations/rescue'),
  );
}

/** A response held until `release()` — the state between a request and its answer. */
function gate() {
  let release: () => void = () => undefined;
  const opened = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { opened, release: () => release() };
}

async function openAppointDialog(options: RescueOptions = {}) {
  const api = open(options);
  await table();
  await userEvent.click(screen.getByRole('button', { name: `Действия: ${ORGANIZATION}` }));
  await userEvent.click(screen.getByRole('menuitem', { name: 'Назначить администратора' }));
  const dialog = await screen.findByRole('dialog', { name: 'Назначение администратора' });
  return { api, dialog };
}

async function chooseVera(dialog: HTMLElement) {
  const field = within(dialog).getByRole('combobox', { name: /Пользователь/ });
  await userEvent.click(field);
  await userEvent.type(field, 'вера');
  await userEvent.click(await screen.findByRole('option', { name: /Вера Иванова/ }));
}

function reasonField(dialog: HTMLElement) {
  return within(dialog).getByRole('textbox', { name: /Причина/ });
}

function posts(api: ReturnType<typeof serveRescue>) {
  return api.requests.filter((request) => request.method === 'POST');
}

describe('QA F-8: «Причина» is trimmed and counted as the server does (BR-RS-5, backend D-1)', () => {
  it('a reason of a byte-order mark and spaces only is «Укажите причину», and nothing is sent', async () => {
    const { api, dialog } = await openAppointDialog();
    await chooseVera(dialog);

    // U+FEFF is what `String.prototype.trim` and, since D-1, the server both remove.
    await userEvent.type(reasonField(dialog), `${BOM}  ${BOM}`);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Назначить' }));

    await waitFor(() => expect(reasonField(dialog)).toHaveAccessibleDescription('Укажите причину'));
    expect(reasonField(dialog)).toHaveFocus();
    expect(posts(api)).toHaveLength(0);
  });

  it('a leading byte-order mark does not count: the mark and 500 letters are sent as the 500 letters', async () => {
    const { api, dialog } = await openAppointDialog();
    await chooseVera(dialog);
    const letters = 'я'.repeat(500);

    // `paste`, not `type`: 501 keystrokes would make the test slow for nothing.
    await userEvent.click(reasonField(dialog));
    await userEvent.paste(`${BOM}${letters}`);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Назначить' }));

    await waitFor(() => expect(posts(api)).toHaveLength(1));
    expect(posts(api)[0]?.body).toEqual({ userId: VERA.id, reason: letters });
  });

  it('501 emoji are refused on the press with «Не более 500 символов», nothing sent', async () => {
    const { api, dialog } = await openAppointDialog();
    await chooseVera(dialog);

    await userEvent.click(reasonField(dialog));
    await userEvent.paste('😀'.repeat(501));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Назначить' }));

    await waitFor(() =>
      expect(reasonField(dialog)).toHaveAccessibleDescription('Не более 500 символов'),
    );
    expect(reasonField(dialog)).toHaveAttribute('aria-invalid', 'true');
    expect(posts(api)).toHaveLength(0);
  });
});

describe('QA F-9: focus goes to the field that carries a server refusal (design § 4.2)', () => {
  // Skipped: qa.md D-6 (open). Remove the skip with the fix.
  it.skip('«Пользователь уже администратор этой организации» puts focus on «Пользователь»', async () => {
    const { dialog } = await openAppointDialog();
    server.use(
      http.post(`${ORGANIZATIONS_URL}/rescue/:organizationId/appointments`, () =>
        problemResponse('organizations.rescue.already_admin'),
      ),
    );
    await chooseVera(dialog);
    await userEvent.type(reasonField(dialog), 'Причина');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Назначить' }));

    await within(dialog).findByText('Пользователь уже администратор этой организации');
    await waitFor(() =>
      expect(within(dialog).getByRole('combobox', { name: /Пользователь/ })).toHaveFocus(),
    );
  });
});

describe('QA F-5: a filter of spaces only is no filter (BR-RS-2)', () => {
  it('asks for every organization, with no `name`, and shows the first page of 120', async () => {
    const api = open();
    await table();

    await userEvent.type(screen.getByRole('textbox', { name: 'Поиск по названию' }), '   ');
    // Past the filter's pause: anything it would send has been sent by now.
    await new Promise((resolve) => setTimeout(resolve, 600));

    expect(listReads(api).every((read) => !read.search.includes('name='))).toBe(true);
    expect(screen.getByText('1–25 из 120')).toBeVisible();
    expect(screen.queryByText(/^Найдено:/)).not.toBeInTheDocument();
  });
});

describe('QA F-6: the page while a page loads, and a page that vanished (design § 3.3)', () => {
  it('keeps the old rows under `aria-busy` while the next page loads, with no skeleton', async () => {
    open();
    const root = await table();
    const held = gate();
    const all = rescueOrganizations();
    server.use(
      http.get(`${ORGANIZATIONS_URL}/rescue`, async () => {
        await held.opened;
        return HttpResponse.json({ count: all.length, data: all.slice(25, 50) });
      }),
    );

    await userEvent.click(screen.getByRole('button', { name: /следующую страницу/i }));

    await waitFor(() => expect(root.closest('[aria-busy="true"]')).not.toBeNull());
    expect(within(root).getByText(ORGANIZATION)).toBeVisible();
    expect(screen.queryByRole('status', { name: /Загрузка данных/ })).not.toBeInTheDocument();
    // The pager's range follows the page state at once, before the rows (no proof of the answer).
    held.release();
    expect(await within(root).findByText('Организация 026')).toBeVisible();
    await waitFor(() => expect(root.closest('[aria-busy="true"]')).toBeNull());
  });

  it('reads the last page that exists when the open one comes back empty', async () => {
    const all = rescueOrganizations().slice(0, 30);
    const api = open({ organizations: all });
    await table();
    // Ten organizations were deleted meanwhile: page 2 is past the end.
    server.use(
      http.get(`${ORGANIZATIONS_URL}/rescue`, ({ request }) => {
        api.requests.push({
          method: 'GET',
          path: new URL(request.url).pathname,
          search: new URL(request.url).search,
        });
        const page = Number(new URL(request.url).searchParams.get('page'));
        return HttpResponse.json({ count: 20, data: page === 1 ? all.slice(0, 20) : [] });
      }),
    );

    await userEvent.click(screen.getByRole('button', { name: /следующую страницу/i }));

    await waitFor(() => expect(screen.getByText('1–20 из 20')).toBeVisible());
    expect(listReads(api).map((read) => read.search)).toEqual(['?page=1', '?page=2', '?page=1']);
    expect(screen.queryByRole('heading', { name: 'Организаций пока нет' })).not.toBeInTheDocument();
  });
});

describe('QA F-6: «Доступ ограничен» stays quiet (DC-8)', () => {
  // Skipped: qa.md D-8 (open). Remove the skip with the fix.
  it.skip('asks for no list after an appointment was refused with 403', async () => {
    const { api, dialog } = await openAppointDialog();
    server.use(
      http.post(`${ORGANIZATIONS_URL}/rescue/:organizationId/appointments`, () =>
        problemResponse('organizations.rescue.forbidden'),
      ),
    );
    await chooseVera(dialog);
    await userEvent.type(reasonField(dialog), 'Причина');
    const before = listReads(api).length;

    await userEvent.click(within(dialog).getByRole('button', { name: 'Назначить' }));
    await screen.findByRole('heading', { name: 'Доступ ограничен' });
    await new Promise((resolve) => setTimeout(resolve, 300));

    expect(listReads(api)).toHaveLength(before);
  });
});

describe('QA F-12: focus in «История назначений» (design § 5)', () => {
  // Skipped: qa.md D-7 (open). Remove the skip with the fix.
  it.skip('leaves focus where the user moved it when the record arrives', async () => {
    open();
    await table();
    const held = gate();
    server.use(
      http.get(`${ORGANIZATIONS_URL}/rescue/:organizationId/appointments`, async () => {
        await held.opened;
        return HttpResponse.json([]);
      }),
    );
    await userEvent.click(screen.getByRole('button', { name: `Действия: ${ORGANIZATION}` }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'История назначений' }));
    const dialog = await screen.findByRole('dialog', { name: /История назначений/ });
    const [titleClose, footerClose] = within(dialog).getAllByRole('button', { name: 'Закрыть' });
    await waitFor(() => expect(footerClose).toHaveFocus());

    titleClose!.focus();
    held.release();
    await within(dialog).findByRole('heading', { name: 'Назначений не было' });

    expect(titleClose).toHaveFocus();
  });
});

describe('QA F-13: the «Участники» host (design § 5)', () => {
  it('returns focus to «История назначений» after «Закрыть»', async () => {
    openPage({ role: 'admin' });
    serveRescue({
      records: {
        [ids.alfa]: [
          {
            id: 'r1',
            appointedAt: '2026-11-03T11:05:00+00:00',
            appointedBy: ROOT_ADMIN,
            appointee: VERA,
            reason: 'Борис уволился',
          },
        ],
      },
    });
    await membersTable();
    const command = screen.getByRole('button', { name: 'История назначений' });

    await userEvent.click(command);
    const dialog = await screen.findByRole('dialog', { name: 'История назначений — «Альфа»' });
    await within(dialog).findByText('Борис уволился');
    await userEvent.click(within(dialog).getAllByRole('button', { name: 'Закрыть' }).at(-1)!);

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() => expect(command).toHaveFocus());
  });
});
