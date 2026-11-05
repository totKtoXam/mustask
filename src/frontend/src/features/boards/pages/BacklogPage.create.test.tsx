import { describe, expect, it } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { processType } from '../../workitems/testFixtures';
import { serveBoards, type BoardsTestApi } from '../testApi';
import { findBacklogTable, identifiers } from '../testBacklog';
import { boardsDataset, ids, platformItem, stateId, storiesDataset } from '../testFixtures';
import { backlogAddress, renderBoards } from '../testRender';

// US-3: creating from a team's backlog with mod-003's dialog (spec § Dialog;
// BR-M4-21; api-contract § 7).

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

/** The header's primary; an empty backlog's frame repeats it. */
async function primary() {
  const [header] = await screen.findAllByRole('button', { name: 'Новый рабочий элемент' });
  if (!header) throw new Error('No «Новый рабочий элемент».');
  return header;
}

async function dialog() {
  return screen.findByRole('dialog', { name: 'Новый рабочий элемент' });
}

function posts(api: BoardsTestApi) {
  return api.writes().filter((request) => request.path.endsWith('/workitems'));
}

/** «Платформа»'s «Истории», reordered: MSK-US-3, MSK-US-9. */
function reorderedStories() {
  const data = storiesDataset(['US-3', 'US-9']);
  data.places = [
    { itemId: data.items[0]?.id ?? '', teamId: ids.platformTeam, level: 'stories', position: 1 },
    { itemId: data.items[1]?.id ?? '', teamId: ids.platformTeam, level: 'stories', position: 2 },
  ];
  return data;
}

describe('US-3 Scenario: The primary offers only the level’s types', () => {
  it('on «Истории» a menu lists «Пользовательская история» and «Баг», each with its icon', async () => {
    serveBoards(storiesDataset());
    renderBoards(backlogAddress('stories'));
    await findBacklogTable();

    const button = await primary();
    expect(button).toHaveAttribute('aria-haspopup', 'menu');
    // The reference's primary draws no trailing chevron; the menu is still announced.
    expect(button.querySelector('[data-testid="ExpandMoreIcon"]')).toBeNull();
    expect(button.querySelector('[data-testid="AddIcon"]')).not.toBeNull();
    await userEvent.click(button);
    const menu = await screen.findByRole('menu', { name: 'Новый рабочий элемент' });
    const entries = within(menu).getAllByRole('menuitem');
    expect(entries.map((entry) => entry.textContent)).toEqual(['Пользовательская история', 'Баг']);
    expect(entries[0]?.querySelector('[data-testid="StoryIcon"]')).not.toBeNull();
    expect(entries[1]?.querySelector('[data-testid="BugReportIcon"]')).not.toBeNull();
    expect(button).toHaveAttribute('aria-expanded', 'true');
  });

  it.each([
    ['epics', 'Эпик'],
    ['features', 'Фича'],
  ])(
    'on «%s» the dialog opens at once with «Тип» «%s», which offers no other type',
    async (level, type) => {
      const data = boardsDataset();
      data.items.push(platformItem(1, level === 'epics' ? 'EPIC' : 'FEAT', 'Уже есть'));
      serveBoards(data);
      renderBoards(backlogAddress(level));
      await findBacklogTable();

      const button = await primary();
      expect(button).not.toHaveAttribute('aria-haspopup');
      await userEvent.click(button);

      expect(screen.queryByRole('menu')).toBeNull();
      const found = await dialog();
      const select = within(found).getByRole('combobox', { name: 'Тип' });
      expect(select).toHaveTextContent(type);
      fireEvent.mouseDown(select);
      const options = await screen.findByRole('listbox');
      expect(
        within(options)
          .getAllByRole('option')
          .map((option) => option.textContent),
      ).toEqual([type]);
    },
  );
});

// mod-017 BR-OT-3, BR-OT-10: a team has no default area; the dialog names the root, starts with the
// team of the backlog it was opened on and sends what it shows. Rewritten over the owning team:
// the first, second and third scenario below.
describe('US-3 Scenario: The item takes the team and the root, and lands last', () => {
  it('names «Новая», the root «Мастаск» and the team; creates with the team; the notice; the row last', async () => {
    const api = serveBoards(reorderedStories());
    renderBoards(backlogAddress());
    const table = await findBacklogTable();
    expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-9']);

    await userEvent.click(await primary());
    await userEvent.click(
      await screen.findByRole('menuitem', { name: 'Пользовательская история' }),
    );
    const found = await dialog();
    expect(
      await within(found).findByText(
        'Рабочий элемент будет создан в статусе «Новая» в области «Мастаск». Остальные поля заполняются в его форме.',
      ),
    ).toBeInTheDocument();
    expect(within(found).getByRole('combobox', { name: 'Тип' })).toHaveTextContent(
      'Пользовательская история',
    );
    const team = await within(found).findByRole('combobox', { name: 'Команда' });
    expect(team).toHaveValue('Платформа');
    expect(within(found).getByText('Встанет в конец бэклога команды «Платформа».')).toBeVisible();

    await userEvent.type(
      within(found).getByRole('textbox', { name: 'Заголовок' }),
      'Экспорт отчёта',
    );
    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Рабочий элемент MSK-US-10 создан.')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).toBeNull();
    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-9', 'MSK-US-10']));
    const [post] = posts(api);
    const body = post?.body as {
      requestId: string;
      typeId: string;
      title: string;
      team: { teamId: string };
    };
    expect(post?.path).toBe(`/api/organizations/${ids.example}/projects/${ids.msk}/workitems`);
    expect(body.team).toEqual({ teamId: ids.platformTeam });
    expect(body).not.toHaveProperty('teamId');
    expect(body.typeId).toBe(processType('US').id);
    expect(body.title).toBe('Экспорт отчёта');
    expect(body.requestId).toMatch(UUID);
    const created = api.item(ids.msk, 10);
    expect(created.areaId).toBe(ids.mskRoot);
    expect(created.teamId).toBe(ids.platformTeam);
    expect(created.stateId).toBe(stateId('US', 'Новая'));
    expect(created.assigneeId).toBeNull();
    expect(api.order(ids.mskTeam)).not.toContain('MSK-US-10');
  });

  it('names a «Баг»’s own initial state as «Тип» changes to it', async () => {
    serveBoards(reorderedStories());
    renderBoards(backlogAddress());
    await findBacklogTable();

    await userEvent.click(await primary());
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Баг' }));
    const found = await dialog();

    expect(within(found).getByRole('combobox', { name: 'Тип' })).toHaveTextContent('Баг');
    expect(
      await within(found).findByText(
        /^Рабочий элемент будет создан в статусе «Новая» в области «Мастаск»/,
      ),
    ).toBeInTheDocument();
  });
});

describe('US-3 Scenario: «Мастаск»’s backlog starts the dialog with «Мастаск»', () => {
  it('a «Фича» with only a title on «Мастаск»’s «Фичи» takes «Мастаск» and is the last row', async () => {
    const data = boardsDataset();
    data.items.push(platformItem(1, 'FEAT', 'Чужая фича', { areaId: ids.mskRoot }));
    const api = serveBoards(data);
    renderBoards(backlogAddress('features', ids.mskTeam));
    const table = await findBacklogTable();

    await userEvent.click(await primary());
    const found = await dialog();
    expect(
      await within(found).findByText(
        'Рабочий элемент будет создан в статусе «Новая» в области «Мастаск». Остальные поля заполняются в его форме.',
      ),
    ).toBeInTheDocument();
    expect(await within(found).findByRole('combobox', { name: 'Команда' })).toHaveValue('Мастаск');
    expect(within(found).getByText('Встанет в конец бэклога команды «Мастаск».')).toBeVisible();
    await userEvent.type(within(found).getByRole('textbox', { name: 'Заголовок' }), 'Отчёты');
    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-FEAT-1', 'MSK-FEAT-2']));
    expect(api.item(ids.msk, 2).areaId).toBe(ids.mskRoot);
    expect(api.item(ids.msk, 2).teamId).toBe(ids.mskTeam);
  });
});

// mod-017 BR-OT-10: retired by name (doc-012 P-1) — «The default area is read when the item is created» —
// «another user’s new default «Платформа / API» is the one the item takes; the others keep «Платформа»»:
// a team has no default area; the item takes the root.

describe('US-3 Scenario: Creation rules stay mod-003’s', () => {
  it('an empty «Заголовок» is refused under the field with nothing sent; a double «Создать» creates one item', async () => {
    const api = serveBoards(reorderedStories());
    renderBoards(backlogAddress());
    const table = await findBacklogTable();
    await userEvent.click(await primary());
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Баг' }));
    const found = await dialog();
    const submit = within(found).getByRole('button', { name: 'Создать' });

    await userEvent.click(submit);
    expect(within(found).getByText('Введите заголовок.')).toBeInTheDocument();
    expect(found).toBeInTheDocument();
    expect(posts(api)).toEqual([]);

    await userEvent.type(within(found).getByRole('textbox', { name: 'Заголовок' }), 'Падение');
    fireEvent.click(submit);
    fireEvent.click(submit);

    await waitFor(() => expect(identifiers(table)).toEqual(['MSK-US-3', 'MSK-US-9', 'MSK-BUG-10']));
    expect(api.data.items.filter((record) => record.title === 'Падение')).toHaveLength(1);
  });
});

describe('US-3 Scenario: The team was deleted while the dialog was open', () => {
  it('shows «Команда не найдена.» under «Команда» (mod-017 BR-OT-13), creates nothing, and a pick clears it', async () => {
    const api = serveBoards(reorderedStories());
    renderBoards(backlogAddress());
    await findBacklogTable();
    await userEvent.click(await primary());
    await userEvent.click(
      await screen.findByRole('menuitem', { name: 'Пользовательская история' }),
    );
    const found = await dialog();
    await within(found).findByRole('combobox', { name: 'Команда' });

    api.data.teams = api.data.teams.map((team) =>
      team.id === ids.platformTeam ? { ...team, active: false } : team,
    );
    await userEvent.type(within(found).getByRole('textbox', { name: 'Заголовок' }), 'Экспорт');
    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    expect(await within(found).findByText('Команда не найдена.')).toBeInTheDocument();
    expect(within(found).getByRole('combobox', { name: 'Команда' })).toHaveFocus();
    expect(within(found).getByRole('textbox', { name: 'Заголовок' })).toHaveValue('Экспорт');
    expect(api.data.items).toHaveLength(2);

    await userEvent.click(within(found).getByRole('combobox', { name: 'Команда' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Мастаск' }));
    expect(within(found).queryByText('Команда не найдена.')).not.toBeInTheDocument();
  });
});

describe('US-3 Scenario: Creating from the empty backlog', () => {
  it('the frame’s «Новый рабочий элемент» opens the same type menu; after a «Баг» the table has one row', async () => {
    const api = serveBoards(storiesDataset());
    renderBoards(backlogAddress('stories', ids.analyticsTeam));
    await screen.findByRole('heading', { level: 2, name: 'Бэклог пуст' });

    const [, frameAction] = screen.getAllByRole('button', { name: 'Новый рабочий элемент' });
    if (!frameAction) throw new Error('No frame action.');
    await userEvent.click(frameAction);
    const menu = await screen.findByRole('menu', { name: 'Новый рабочий элемент' });
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((entry) => entry.textContent),
    ).toEqual(['Пользовательская история', 'Баг']);
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Баг' }));
    const found = await dialog();
    expect(await within(found).findByText(/в области «Мастаск»/)).toBeInTheDocument();
    expect(await within(found).findByRole('combobox', { name: 'Команда' })).toHaveValue(
      'Аналитика',
    );
    expect(within(found).getByText('Встанет в конец бэклога команды «Аналитика».')).toBeVisible();
    await userEvent.type(
      within(found).getByRole('textbox', { name: 'Заголовок' }),
      'Сбой выгрузки',
    );
    await userEvent.click(within(found).getByRole('button', { name: 'Создать' }));

    expect(identifiers(await findBacklogTable())).toEqual(['MSK-BUG-12']);
    expect(screen.queryByRole('heading', { level: 2, name: 'Бэклог пуст' })).toBeNull();
    expect(api.item(ids.msk, 12).areaId).toBe(ids.mskRoot);
    expect(api.item(ids.msk, 12).teamId).toBe(ids.analyticsTeam);
  });
});
