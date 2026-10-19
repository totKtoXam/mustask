import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { gate } from '../../../test/gate';
import { server } from '../../../test/mocks/server';
import { serveWorkItems } from '../testApi';
import {
  ASSIGNEE_OPTIONS_URL,
  emptyDataset,
  ids,
  item,
  listFilterDataset,
  problem,
  type Dataset,
} from '../testFixtures';
import { MSK_LIST, renderWorkItems } from '../testRender';

// feat-003 US-2, BR-WS-6..-8, design § 2.3, § 4, D-1 A, D-2 A, OQ-2 A: the «Исполнитель» filter —
// the kit's multi-picker over the people assigned to the project's items, former members and
// disabled accounts included, the marks in the options, the tags and the list's column.

vi.setConfig({ testTimeout: 120_000 });

const enc = encodeURIComponent;

const band = () => screen.getByRole('group', { name: 'Фильтры' });
const table = () => screen.findByRole('table', { name: 'Рабочие элементы' });
const rowsOf = (element: HTMLElement) => within(element).getAllByRole('row').slice(1);
const cellsOf = (row: HTMLElement) =>
  within(row)
    .getAllByRole('cell')
    .map((cell) => cell.textContent ?? '');
const addressSearch = () => screen.getByTestId('location').getAttribute('data-search') ?? '';
const field = () => within(band()).getByRole('combobox', { name: 'Исполнитель' });

async function listed(): Promise<string[]> {
  return rowsOf(await table()).map((row) => cellsOf(row)[0] ?? '');
}

async function expectListed(expected: string[]) {
  await waitFor(async () => expect(await listed()).toEqual(expected));
}

async function openList() {
  await userEvent.click(field());
  return screen.findByRole('listbox');
}

const optionTexts = (list: HTMLElement) =>
  within(list)
    .getAllByRole('option')
    .map((option) => option.textContent);

async function closeList() {
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
}

async function chooseAssignees(...labels: string[]) {
  await openList();
  for (const label of labels) {
    await userEvent.click(await screen.findByRole('option', { name: label }));
  }
  await closeList();
}

async function chooseStates(...names: string[]) {
  await userEvent.click(within(band()).getByRole('combobox', { name: 'Статус' }));
  for (const name of names) {
    await userEvent.click(await screen.findByRole('option', { name }));
  }
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
}

const ALL = ['MSK-BUG-42', 'MSK-US-3', 'MSK-TASK-4', 'MSK-BUG-2', 'MSK-US-1'];

describe('feat-003 BR-WS-6 the options of «Исполнитель»', () => {
  it('are the people assigned to the project’s live items, former members and disabled accounts among them, by name, then «Не назначен», with no counts', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    const list = await openList();

    expect(list).toHaveAttribute('aria-multiselectable', 'true');
    expect(optionTexts(list)).toEqual([
      'Иван Котов (не в организации)',
      'Мия Петрова',
      'Пётр Иванов (неактивен)',
      'Не назначен',
    ]);
  });

  it('offer nobody who is assigned nothing, and nobody of another project', async () => {
    const data = listFilterDataset();
    data.items.push(item(ids.ops, 1, 'BUG', 'Чужая', { assigneeId: ids.annaK }));
    serveWorkItems(data);
    renderWorkItems(MSK_LIST);
    await table();

    const list = await openList();

    expect(optionTexts(list)).not.toContain('Анна Смирнова');
    expect(optionTexts(list)).not.toContain('Анна Кузнецова');
  });

  it('bring in the people of the deleted items while «Удалена» is chosen, and read again for it', async () => {
    const api = serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();
    expect(optionTexts(await openList())).not.toContain('Зоя Ким');
    await closeList();

    await chooseStates('Удалена');
    await waitFor(() =>
      expect(api.reads('assignee-options').at(-1)?.path).toContain(`state=${enc('Удалена')}`),
    );

    expect(optionTexts(await openList())).toEqual([
      'Зоя Ким',
      'Иван Котов (не в организации)',
      'Мия Петрова',
      'Пётр Иванов (неактивен)',
      'Не назначен',
    ]);
  });

  it('keep the previous options while the new ones come', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();
    const held = gate();
    server.use(
      http.get(ASSIGNEE_OPTIONS_URL, async ({ request }) => {
        if (new URL(request.url).searchParams.has('state')) {
          await held.opened;
        }
        return undefined;
      }),
    );

    await chooseStates('Удалена');
    const list = await openList();

    expect(optionTexts(list)).toContain('Мия Петрова');
    expect(optionTexts(list)).not.toContain('Зоя Ким');
    held.open();
    expect(await screen.findByRole('option', { name: 'Зоя Ким' })).toBeInTheDocument();
  });

  it('say «Загрузка…» while the read is on, and the field works meanwhile', async () => {
    serveWorkItems(listFilterDataset());
    const held = gate();
    server.use(
      http.get(ASSIGNEE_OPTIONS_URL, async () => {
        await held.opened;
        return undefined;
      }),
    );
    renderWorkItems(MSK_LIST);
    await table();

    await userEvent.click(field());

    expect(await screen.findByText('Загрузка…')).toBeInTheDocument();
    held.open();
    expect(await screen.findByRole('option', { name: 'Мия Петрова' })).toBeInTheDocument();
  });

  it('say why when the read failed, and opening the list again reads again', async () => {
    serveWorkItems(listFilterDataset());
    const held = gate();
    let failing = true;
    server.use(
      http.get(ASSIGNEE_OPTIONS_URL, async () => {
        await held.opened;
        if (failing) {
          failing = false;
          return HttpResponse.json(problem('workitems.validation_failed'), { status: 500 });
        }
        return undefined;
      }),
    );
    renderWorkItems(MSK_LIST);
    await table();

    await userEvent.click(field());
    expect(await screen.findByText('Загрузка…')).toBeInTheDocument();
    held.open();
    expect(
      await screen.findByText('Не удалось загрузить исполнителей. Попробуйте ещё раз.'),
    ).toBeInTheDocument();
    await closeList();

    expect(optionTexts(await openList())).toContain('Мия Петрова');
  });

  it('are never empty before a search: «Не назначен» stands even when nobody is assigned', async () => {
    const data = emptyDataset();
    data.items.push(item(ids.msk, 1, 'TASK', 'Ничья'));
    serveWorkItems(data);
    renderWorkItems(MSK_LIST);
    await table();

    expect(optionTexts(await openList())).toEqual(['Не назначен']);
  });
});

describe('feat-003 BR-WS-8 the match', () => {
  it('a chosen person narrows the rows and the count, shows as a tag with a remove button, and is written to the address', async () => {
    const api = serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await chooseAssignees('Мия Петрова');

    await expectListed(['MSK-BUG-42', 'MSK-US-1']);
    expect(screen.getByText('1–2 из 2')).toBeInTheDocument();
    expect(api.reads('/workitems?').at(-1)?.path).toContain(`assigneeId=${ids.mia}`);
    expect(addressSearch()).toBe(`?assigneeId=${ids.mia}`);
    expect(
      within(band()).getByRole('button', { name: 'Убрать «Мия Петрова»' }),
    ).toBeInTheDocument();
  });

  it('«Не назначен» matches the items with no assignee, and any chosen value matches', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await chooseAssignees('Не назначен');
    await expectListed(['MSK-US-3']);
    expect(addressSearch()).toBe('?assigneeId=none');

    await chooseAssignees('Мия Петрова');
    await expectListed(['MSK-BUG-42', 'MSK-US-3', 'MSK-US-1']);
    expect(addressSearch()).toBe(`?assigneeId=none&assigneeId=${ids.mia}`);
  });

  it('a former member can be chosen, and the deleted item of the same person stays out until «Удалена» is chosen', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await chooseAssignees('Иван Котов (не в организации)');
    await expectListed(['MSK-BUG-2']);

    await chooseStates('Удалена');
    await expectListed(['MSK-US-5']);
  });

  it('a disabled account can be chosen', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await chooseAssignees('Пётр Иванов (неактивен)');

    await expectListed(['MSK-TASK-4']);
  });

  it('removing a tag with its button, and with Backspace in the empty input, takes the person out of the address', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();
    await chooseAssignees('Мия Петрова', 'Не назначен');
    await expectListed(['MSK-BUG-42', 'MSK-US-3', 'MSK-US-1']);

    await userEvent.click(within(band()).getByRole('button', { name: 'Убрать «Мия Петрова»' }));
    await expectListed(['MSK-US-3']);
    expect(addressSearch()).toBe('?assigneeId=none');

    field().focus();
    await userEvent.keyboard('{Backspace}');
    await expectListed(ALL);
    expect(addressSearch()).toBe('');
  });
});

describe('feat-003 BR-WS-7, OQ-2 A how a person reads, in the options, the tags and the list’s column', () => {
  it('the column reads the same words as the options: the marks of mod-005’s one helper', async () => {
    const data = listFilterDataset();
    data.items.push(item(ids.msk, 7, 'TASK', 'Забытая', { assigneeId: ids.ghost }));
    serveWorkItems(data);
    renderWorkItems(MSK_LIST);

    const assignees = rowsOf(await table()).map((row) => cellsOf(row)[3]);

    expect(assignees).toContain('МПМия Петрова');
    expect(assignees).toContain('ИКИван Котов (не в организации)');
    expect(assignees).toContain('ПИПётр Иванов (неактивен)');
    expect(assignees).toContain('Не назначен');
    expect(assignees).toContain('Неизвестный пользователь');
    expect(within(await table()).queryByText('Неизвестный пользователь (неактивен)')).toBeNull();
  });

  it('a user the directory does not know and who left reads «Неизвестный пользователь (не в организации)»', async () => {
    const data = emptyDataset();
    data.formerMemberIds = [ids.ghost];
    data.items.push(item(ids.msk, 1, 'TASK', 'Забытая', { assigneeId: ids.ghost }));
    serveWorkItems(data);
    renderWorkItems(MSK_LIST);

    const row = rowsOf(await table())[0] as HTMLElement;
    expect(cellsOf(row)[3]).toBe('Неизвестный пользователь (не в организации)');
    expect(optionTexts(await openList())).toEqual([
      'Неизвестный пользователь (не в организации)',
      'Не назначен',
    ]);
  });

  it('the tag reads the option’s text, and the remove button names it', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await chooseAssignees('Иван Котов (не в организации)', 'Пётр Иванов (неактивен)');

    expect(
      within(band()).getByRole('button', { name: 'Убрать «Иван Котов (не в организации)»' }),
    ).toBeInTheDocument();
    expect(
      within(band()).getByRole('button', { name: 'Убрать «Пётр Иванов (неактивен)»' }),
    ).toBeInTheDocument();
  });
});

/** Two people of one name, two unknown users who left, and a name with «ё». */
function alikeDataset(): Dataset {
  const data = emptyDataset();
  const ivanA = 'u0000000-ivan-a';
  const ivanB = 'u0000000-ivan-b';
  const artem = 'u0000000-artem';
  data.users.push(
    { id: ivanA, displayName: 'Иван Котов', email: 'ivan.k@alfa.test', active: true },
    { id: ivanB, displayName: 'Иван Котов', email: 'i.kotov@beta.test', active: true },
    { id: artem, displayName: 'Артём Фёдоров', email: 'artem@alfa.test', active: true },
  );
  data.formerMemberIds = ['u0000000-ghost-1', 'u0000000-ghost-2'];
  data.items.push(
    item(ids.msk, 1, 'TASK', 'Первая', { assigneeId: ivanA }),
    item(ids.msk, 2, 'TASK', 'Вторая', { assigneeId: ivanB }),
    item(ids.msk, 3, 'TASK', 'Третья', { assigneeId: artem }),
    item(ids.msk, 4, 'TASK', 'Четвёртая', { assigneeId: 'u0000000-ghost-1' }),
    item(ids.msk, 5, 'TASK', 'Пятая', { assigneeId: 'u0000000-ghost-2' }),
  );
  return data;
}

describe('feat-003 D-2 options that read alike', () => {
  it('are told apart in the text itself, and only they: the e-mail for people who differ by it, a number for unknown users', async () => {
    serveWorkItems(alikeDataset());
    renderWorkItems(MSK_LIST);
    await table();

    expect(optionTexts(await openList())).toEqual([
      'Артём Фёдоров',
      'Иван Котов — ivan.k@alfa.test',
      'Иван Котов — i.kotov@beta.test',
      'Неизвестный пользователь № 1 (не в организации)',
      'Неизвестный пользователь № 2 (не в организации)',
      'Не назначен',
    ]);
  });

  it('the tag and the remove button carry the same text, and the choice picks that person’s items', async () => {
    serveWorkItems(alikeDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await chooseAssignees('Иван Котов — i.kotov@beta.test');

    await expectListed(['MSK-TASK-2']);
    expect(
      within(band()).getByRole('button', {
        name: 'Убрать «Иван Котов — i.kotov@beta.test»',
      }),
    ).toBeInTheDocument();
  });

  it('typing narrows by the text, ignoring letter case and accents («ё» finds «е»), and an alike option is found by its e-mail', async () => {
    serveWorkItems(alikeDataset());
    renderWorkItems(MSK_LIST);
    await table();
    await openList();

    await userEvent.type(field(), 'федоров');
    expect(optionTexts(screen.getByRole('listbox'))).toEqual(['Артём Фёдоров']);

    await userEvent.clear(field());
    await userEvent.type(field(), 'BETA');
    expect(optionTexts(screen.getByRole('listbox'))).toEqual(['Иван Котов — i.kotov@beta.test']);

    await userEvent.clear(field());
    await userEvent.type(field(), 'никого такого');
    expect(await screen.findByText('Никого не найдено')).toBeInTheDocument();
  });
});

describe('feat-003 BR-WS-10 an id the options do not name', () => {
  it('stays in the address, is no tag, and the field reads «Фильтр задан» while nothing else is chosen', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?assigneeId=u0000000-nobody`);

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
    ).toBeVisible();
    expect(within(band()).getByText('Фильтр задан')).toBeInTheDocument();
    expect(within(band()).queryByRole('button', { name: /^Убрать/ })).toBeNull();

    await chooseAssignees('Мия Петрова');

    expect(addressSearch()).toBe(`?assigneeId=u0000000-nobody&assigneeId=${ids.mia}`);
    await expectListed(['MSK-BUG-42', 'MSK-US-1']);
  });

  it('a chosen person who is no longer an option leaves the tags once the options arrive, and the address keeps the id', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?state=${enc('Удалена')}&assigneeId=${ids.zoya}`);
    await expectListed(['MSK-FEAT-6']);
    expect(
      await within(band()).findByRole('button', { name: 'Убрать «Зоя Ким»' }),
    ).toBeInTheDocument();

    await userEvent.click(within(band()).getByRole('combobox', { name: 'Статус' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Удалена' }));
    await userEvent.keyboard('{Escape}');

    await waitFor(() =>
      expect(within(band()).queryByRole('button', { name: 'Убрать «Зоя Ким»' })).toBeNull(),
    );
    expect(addressSearch()).toBe(`?assigneeId=${ids.zoya}`);
    expect(within(band()).getByText('Фильтр задан')).toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' }),
    ).toBeVisible();
  });
});

describe('feat-003 design § 9 the keys', () => {
  it('Down opens, typing narrows, Down and Enter tick, the list stays open, Esc closes with the focus on the field', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();
    field().focus();

    await userEvent.keyboard('{ArrowDown}');
    await screen.findByRole('listbox');
    await userEvent.keyboard('миа');
    await userEvent.keyboard('{Backspace}я');
    expect(optionTexts(screen.getByRole('listbox'))).toEqual(['Мия Петрова']);
    await userEvent.keyboard('{ArrowDown}{Enter}');

    await expectListed(['MSK-BUG-42', 'MSK-US-1']);
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
    expect(field()).toHaveFocus();
  });
});

describe('feat-003 D-1 A a list of 2 000 people', () => {
  it('opens, narrows by typing and ticks without the pointer, measured', async () => {
    const data = emptyDataset();
    for (let number = 1; number <= 2000; number += 1) {
      const id = `u0000000-person-${String(number).padStart(4, '0')}`;
      data.users.push({
        id,
        displayName: `Сотрудник ${String(number).padStart(4, '0')}`,
        email: `person${number}@alfa.test`,
        active: true,
      });
      data.items.push(item(ids.msk, number, 'TASK', `Задача ${number}`, { assigneeId: id }));
    }
    serveWorkItems(data);
    renderWorkItems(MSK_LIST);
    await table();

    const opening = performance.now();
    const list = await openList();
    await within(list).findByRole('option', { name: 'Сотрудник 2000' });
    const opened = performance.now() - opening;
    expect(within(list).getAllByRole('option')).toHaveLength(2001);

    const typing = performance.now();
    await userEvent.type(field(), '1999');
    await waitFor(() =>
      expect(within(screen.getByRole('listbox')).getAllByRole('option')).toHaveLength(1),
    );
    const narrowed = performance.now() - typing;

    await userEvent.keyboard('{ArrowDown}{Enter}');
    await waitFor(() => expect(addressSearch()).toContain('assigneeId=u0000000-person-1999'));
    await closeList();

    // jsdom is no browser: the numbers are a floor for regressions and are reported to the human
    // (design § 11), not a promise of the screen's speed.
    process.stdout.write(
      `feat-003 D-1 measured (jsdom): open 2000 options ${Math.round(opened)} ms, narrow by typing ${Math.round(narrowed)} ms\n`,
    );
    expect(opened).toBeLessThan(100_000);
    expect(narrowed).toBeLessThan(100_000);
  }, 300_000);
});
