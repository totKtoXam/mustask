import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { ADMIN_ORGANIZATIONS_PATH } from '../paths';
import { organizationsPermissions } from '../permissions';
import { ORGANIZATIONS_URL } from '../testFixtures';
import { renderPage } from '../testRender';
import {
  ACTING,
  VERA,
  problemResponse,
  rescueIds,
  serveRescue,
  type RescueOptions,
} from '../testRescue';

// feat-033 US-2 (design § 4, § 6): «Назначение администратора» — the lead and the warning, the
// appointee search with no organization, the reason checked on the press in code points, every
// refusal in its place with the catalog's Russian text, success.

const ORGANIZATION = 'Организация 001';

async function openDialog(options: RescueOptions = {}) {
  const api = serveRescue(options);
  renderPage({
    path: ADMIN_ORGANIZATIONS_PATH,
    permissions: [organizationsPermissions.organizationRescue],
  });
  await screen.findByRole('table', { name: 'Все организации' });
  await userEvent.click(screen.getByRole('button', { name: `Действия: ${ORGANIZATION}` }));
  await userEvent.click(screen.getByRole('menuitem', { name: 'Назначить администратора' }));
  const dialog = await screen.findByRole('dialog', { name: 'Назначение администратора' });
  return { api, dialog };
}

async function chooseUser(dialog: HTMLElement, text: string, option: RegExp) {
  const field = within(dialog).getByRole('combobox', { name: /Пользователь/ });
  await userEvent.click(field);
  await userEvent.type(field, text);
  await userEvent.click(await screen.findByRole('option', { name: option }));
}

function reasonField(dialog: HTMLElement) {
  return within(dialog).getByRole('textbox', { name: /Причина/ });
}

function posts(api: ReturnType<typeof serveRescue>) {
  return api.requests.filter((request) => request.method === 'POST');
}

describe('feat-033 US-2: the dialog', () => {
  it('opens on «Пользователь» with the lead, the Keycloak warning and «Назначить» disabled', async () => {
    const { dialog } = await openDialog();

    await waitFor(() =>
      expect(within(dialog).getByRole('combobox', { name: /Пользователь/ })).toHaveFocus(),
    );
    expect(
      within(dialog).getByText(
        `Выбранный пользователь станет администратором организации «${ORGANIZATION}». Никто не будет удалён, ничья роль не будет понижена. Вы не получите доступа к содержимому организации.`,
      ),
    ).toBeVisible();
    expect(
      within(dialog).getByText(
        'MUSTASK не видит, отключена ли учётная запись в Keycloak: убедитесь, что пользователь может войти.',
      ),
    ).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Назначить' })).toBeDisabled();
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toBeEnabled();
  });

  it('searches the appointee with no organization in the address, from two characters', async () => {
    const { api, dialog } = await openDialog();
    const field = within(dialog).getByRole('combobox', { name: /Пользователь/ });

    await userEvent.click(field);
    await userEvent.type(field, 'в');
    expect(await screen.findByText('Введите не менее 2 символов имени или почты')).toBeVisible();
    await userEvent.type(field, 'е');
    expect(await screen.findByRole('option', { name: /Вера Иванова/ })).toBeVisible();

    const searches = api.requests.filter((request) =>
      request.path.includes('appointee-candidates'),
    );
    expect(searches).toHaveLength(1);
    expect(searches[0]?.path).toBe('/api/organizations/rescue/appointee-candidates');
    expect(searches[0]?.search).toBe(`?text=${encodeURIComponent('ве')}`);
  });

  it('closes with nothing sent on «Отмена»', async () => {
    const { api, dialog } = await openDialog();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(posts(api)).toHaveLength(0);
  });
});

describe('feat-033 US-2: the reason', () => {
  it('is required: «Назначить» is enabled once a user is chosen, the press says «Укажите причину» and sends nothing', async () => {
    const { api, dialog } = await openDialog();
    await chooseUser(dialog, 'вера', /Вера Иванова/);
    const submit = within(dialog).getByRole('button', { name: 'Назначить' });
    expect(submit).toBeEnabled();

    await userEvent.type(reasonField(dialog), '   ');
    await userEvent.click(submit);

    expect(await within(dialog).findByText('Укажите причину')).toBeVisible();
    expect(reasonField(dialog)).toHaveFocus();
    expect(reasonField(dialog)).toHaveAttribute('aria-invalid', 'true');
    expect(posts(api)).toHaveLength(0);
  });

  it('counts code points of the trimmed text: 501 refused, 500 emoji (1000 UTF-16 units) sent', async () => {
    const { api, dialog } = await openDialog();
    await chooseUser(dialog, 'вера', /Вера Иванова/);
    const submit = within(dialog).getByRole('button', { name: 'Назначить' });

    await userEvent.click(reasonField(dialog));
    await userEvent.paste(`  ${'😀'.repeat(501)}  `);
    await userEvent.click(submit);
    await waitFor(() => expect(reasonField(dialog)).toHaveAttribute('aria-invalid', 'true'));
    // The hint carries the same words; the error replaces it as the field's description.
    expect(reasonField(dialog)).toHaveAccessibleDescription('Не более 500 символов');
    expect(posts(api)).toHaveLength(0);

    await userEvent.clear(reasonField(dialog));
    await userEvent.paste(`  ${'😀'.repeat(500)}  `);
    await userEvent.click(submit);

    await waitFor(() => expect(posts(api)).toHaveLength(1));
    expect((posts(api)[0]?.body as { reason: string }).reason).toBe('😀'.repeat(500));
  });

  it('sends the trimmed reason with the chosen user to the organization’s appointments route', async () => {
    const { api, dialog } = await openDialog();
    await chooseUser(dialog, 'вера', /Вера Иванова/);

    await userEvent.type(reasonField(dialog), '  Борис уволился  ');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Назначить' }));

    await waitFor(() => expect(posts(api)).toHaveLength(1));
    expect(posts(api)[0]?.path).toBe(`/api/organizations/rescue/${rescueIds.plain}/appointments`);
    expect(posts(api)[0]?.body).toEqual({ userId: VERA.id, reason: 'Борис уволился' });
  });
});

describe('feat-033 US-2: success', () => {
  it('closes, names the appointee in the notice, reads the list again and returns focus to the row', async () => {
    const { api, dialog } = await openDialog();
    await chooseUser(dialog, 'вера', /Вера Иванова/);
    await userEvent.type(reasonField(dialog), 'Борис уволился');
    const readsBefore = api.requests.filter(
      (request) => request.method === 'GET' && request.path === '/api/organizations/rescue',
    ).length;

    await userEvent.click(within(dialog).getByRole('button', { name: 'Назначить' }));

    expect(await screen.findByText('Администратор назначен: Вера Иванова')).toBeVisible();
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() =>
      expect(screen.getByRole('button', { name: `Действия: ${ORGANIZATION}` })).toHaveFocus(),
    );
    await waitFor(() =>
      expect(
        api.requests.filter(
          (request) => request.method === 'GET' && request.path === '/api/organizations/rescue',
        ).length,
      ).toBeGreaterThan(readsBefore),
    );
  });
});

describe('feat-033 US-2: the refusals, by code and in the catalog’s words', () => {
  async function refused(
    code: string,
    wrongDetail = 'Текст сервера, который панель не показывает',
  ) {
    const { dialog } = await openDialog({
      users: [VERA, { ...VERA, id: ACTING, displayName: 'Сам Себя' }],
    });
    server.use(
      http.post(`${ORGANIZATIONS_URL}/rescue/:organizationId/appointments`, () =>
        problemResponse(code, wrongDetail),
      ),
    );
    await chooseUser(dialog, 'вера', /Вера Иванова/);
    await userEvent.type(reasonField(dialog), 'Причина');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Назначить' }));
    return dialog;
  }

  it.each([
    [
      'organizations.rescue.user_unavailable',
      'Этого пользователя нельзя назначить администратором',
    ],
    ['organizations.rescue.self_appointment', 'Нельзя назначить администратором себя'],
    ['organizations.rescue.already_admin', 'Пользователь уже администратор этой организации'],
  ])('%s lands on «Пользователь», which keeps the chosen user', async (code, text) => {
    const dialog = await refused(code);

    const field = within(dialog).getByRole('combobox', { name: /Пользователь/ });
    expect(await within(dialog).findByText(text)).toBeVisible();
    expect(field).toHaveValue('Вера Иванова');
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(reasonField(dialog)).toHaveValue('Причина');
    expect(screen.queryByText('Администратор назначен: Вера Иванова')).not.toBeInTheDocument();
  });

  it('clears the user refusal as the user changes', async () => {
    const dialog = await refused('organizations.rescue.already_admin');
    await within(dialog).findByText('Пользователь уже администратор этой организации');

    await userEvent.clear(within(dialog).getByRole('combobox', { name: /Пользователь/ }));

    await waitFor(() =>
      expect(
        within(dialog).queryByText('Пользователь уже администратор этой организации'),
      ).not.toBeInTheDocument(),
    );
  });

  it.each([
    ['organizations.rescue.reason_required', 'Укажите причину'],
    ['organizations.rescue.reason_too_long', 'Не более 500 символов'],
    ['organizations.rescue.reason_invalid', 'Причина содержит недопустимые символы'],
  ])('a server %s lands on «Причина» (a miscount, an API caller)', async (code, text) => {
    const { dialog } = await openDialog();
    const body = {
      status: 400,
      title: 'x',
      detail: 'Текст сервера, который панель не показывает',
      errorCode: 'organizations.validation_failed',
      errors: { reason: [{ code, detail: 'Другой текст' }] },
    };
    server.use(
      http.post(`${ORGANIZATIONS_URL}/rescue/:organizationId/appointments`, () =>
        HttpResponse.json(body, {
          status: 400,
          headers: { 'Content-Type': 'application/problem+json' },
        }),
      ),
    );
    await chooseUser(dialog, 'вера', /Вера Иванова/);
    await userEvent.type(reasonField(dialog), 'Причина');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Назначить' }));

    await waitFor(() => expect(reasonField(dialog)).toHaveAttribute('aria-invalid', 'true'));
    expect(reasonField(dialog)).toHaveAccessibleDescription(text);
  });

  it('an organization that is gone shows «Организация не найдена», disables «Назначить» and reads the list again', async () => {
    const { api, dialog } = await openDialog();
    server.use(
      http.post(`${ORGANIZATIONS_URL}/rescue/:organizationId/appointments`, () =>
        problemResponse('organizations.organization.not_found', 'Иной текст'),
      ),
    );
    await chooseUser(dialog, 'вера', /Вера Иванова/);
    await userEvent.type(reasonField(dialog), 'Причина');
    const readsBefore = api.requests.filter(
      (request) => request.method === 'GET' && request.path === '/api/organizations/rescue',
    ).length;

    await userEvent.click(within(dialog).getByRole('button', { name: 'Назначить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Организация не найдена');
    expect(within(dialog).getByRole('button', { name: 'Назначить' })).toBeDisabled();
    await waitFor(() =>
      expect(
        api.requests.filter(
          (request) => request.method === 'GET' && request.path === '/api/organizations/rescue',
        ).length,
      ).toBeGreaterThan(readsBefore),
    );
  });

  it('a 403 turns the page into «Доступ ограничен» and closes the dialog (DC-8)', async () => {
    const { dialog } = await openDialog();
    server.use(
      http.post(`${ORGANIZATIONS_URL}/rescue/:organizationId/appointments`, () =>
        problemResponse('organizations.rescue.forbidden'),
      ),
    );
    await chooseUser(dialog, 'вера', /Вера Иванова/);
    await userEvent.type(reasonField(dialog), 'Причина');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Назначить' }));

    expect(await screen.findByRole('heading', { name: 'Доступ ограничен' })).toBeVisible();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('a 500 shows the server’s text in the dialog’s notice and keeps the fields', async () => {
    const { dialog } = await openDialog();
    server.use(
      http.post(`${ORGANIZATIONS_URL}/rescue/:organizationId/appointments`, () =>
        HttpResponse.json(
          { status: 500, title: 'x', detail: 'Сервис временно недоступен' },
          { status: 500, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    await chooseUser(dialog, 'вера', /Вера Иванова/);
    await userEvent.type(reasonField(dialog), 'Причина');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Назначить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Сервис временно недоступен',
    );
    expect(reasonField(dialog)).toHaveValue('Причина');
    expect(within(dialog).getByRole('button', { name: 'Назначить' })).toBeEnabled();
  });
});
