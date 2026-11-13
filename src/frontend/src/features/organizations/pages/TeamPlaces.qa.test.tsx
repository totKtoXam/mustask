import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ORG_TEAMS_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

// mod-021 frontend QA (doc-016 US-1; BR-TD-1, BR-TD-4 team part): the refusals and the offers the developer's
// tests leave out — the server's department answers on the two forms, the picker's offer, the unlink dialog's
// cancel, and the dead end of a team linked only to a deactivated project (qa.md, F-1).

const ALFA_TEAMS = `/organizations/${ids.alfa}/teams`;

async function openMove(teamId: string) {
  renderPage({ path: `/organizations/${ids.alfa}/teams/${teamId}/overview`, role: 'admin' });
  await screen.findByRole('heading', { level: 1, name: /^Команда: / });
  await userEvent.click(await screen.findByRole('button', { name: 'Переместить' }));
  return screen.findByRole('dialog', { name: /^Перемещение команды/ });
}

async function chooseDepartment(dialog: HTMLElement, name: string) {
  await userEvent.click(within(dialog).getByRole('combobox', { name: /Новый департамент/ }));
  await userEvent.click(await screen.findByRole('option', { name }));
}

describe('mod-021 QA: «Перемещение команды» refusals', () => {
  it('a department deleted meanwhile (404 `area.not_found`) lands under the field, which drops the choice', async () => {
    serveOrganizations();
    const dialog = await openMove(ids.anlTeam);
    await chooseDepartment(dialog, 'Отчёты');
    serveProblem(
      'put',
      `${ORG_TEAMS_URL}/${ids.anlTeam}/department`,
      problem('organizations.area.not_found'),
    );

    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await within(dialog).findByText('Область не найдена.')).toBeVisible();
    expect(screen.getByRole('dialog', { name: /^Перемещение команды/ })).toBeVisible();
  });

  it('a 403 is the dialog’s alert and the dialog stays', async () => {
    serveOrganizations();
    const dialog = await openMove(ids.anlTeam);
    await chooseDepartment(dialog, 'Отчёты');
    serveProblem(
      'put',
      `${ORG_TEAMS_URL}/${ids.anlTeam}/department`,
      problem('organizations.access.forbidden'),
    );

    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Недостаточно прав');
  });

  it('«Отмена» of the unlink dialog returns to the move dialog with the refusal and the department kept', async () => {
    const api = serveOrganizations();
    const dialog = await openMove(ids.platformTeam);
    await chooseDepartment(dialog, 'Отчёты');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));
    await within(dialog).findByText(
      'Команда связана с проектами. Отключите её от них перед перемещением.',
    );
    await userEvent.click(
      within(dialog).getByRole('button', { name: 'Отключить от проекта «Пример проекта»' }),
    );
    const unlink = await screen.findByRole('dialog', { name: 'Отключение команды от проекта' });

    await userEvent.click(within(unlink).getByRole('button', { name: 'Отмена' }));

    const again = await screen.findByRole('dialog', { name: /^Перемещение команды/ });
    expect(within(again).getByRole('combobox', { name: /Новый департамент/ })).toHaveValue(
      'Отчёты',
    );
    expect(
      within(again).getByText(
        'Команда связана с проектами. Отключите её от них перед перемещением.',
      ),
    ).toBeVisible();
    // Nothing was unlinked, nothing moved.
    expect(api.writes().map((write) => write.method)).toEqual(['PUT']);
  });

  // F-1 (qa.md, major): the server refuses a move for a link to a project of ANY status; the deactivated
  // ones cannot be unlinked, so the dialog names them, tagged, and says to restore the project first.
  it('a team linked only to a deactivated project: the refusal names it, tagged, with no «Отключить» and the way out (F-1)', async () => {
    serveOrganizations();
    const dialog = await openMove(ids.oldTeam);
    await chooseDepartment(dialog, 'Отчёты');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Переместить' }));
    await within(dialog).findByText(
      'Команда связана с проектами. Отключите её от них перед перемещением.',
    );

    expect(await within(dialog).findByText('Старый проект')).toBeVisible();
    expect(within(dialog).getByText('деактивирован')).toBeVisible();
    expect(within(dialog).getByText(/Сначала восстановите проект/)).toBeVisible();
    expect(within(dialog).queryByRole('button', { name: /^Отключить/ })).not.toBeInTheDocument();
  });
});

describe('mod-021 QA: «Новая команда», the department field', () => {
  async function openCreate() {
    const api = serveOrganizations();
    renderPage({ path: ALFA_TEAMS, role: 'admin' });
    await screen.findByRole('table', { name: 'Команды' });
    await userEvent.click(screen.getByRole('button', { name: 'Создать команду' }));
    const dialog = await screen.findByRole('dialog', { name: 'Новая команда' });
    return { api, dialog };
  }

  it('offers departments only: never a project node, a sub-area or «Верхний уровень» (US-1)', async () => {
    const { dialog } = await openCreate();

    await userEvent.click(within(dialog).getByRole('combobox', { name: /Департамент/ }));
    const options = within(await screen.findByRole('listbox')).getAllByRole('option');

    expect(options.map((option) => option.textContent)).toEqual(['Отчёты', 'Платформа']);
  });

  it('a department deleted meanwhile (404 `area.not_found`) lands under «Департамент», nothing is created', async () => {
    const { api, dialog } = await openCreate();
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Название' }), 'Ядро');
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Департамент/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Отчёты' }));
    serveProblem('post', ORG_TEAMS_URL, problem('organizations.area.not_found'));

    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await within(dialog).findByText('Область не найдена.')).toBeVisible();
    expect(api.writes()).toEqual([]);
    expect(screen.getByRole('dialog', { name: 'Новая команда' })).toBeVisible();
  });
});
