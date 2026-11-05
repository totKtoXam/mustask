import { afterEach, describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { WORK_ITEM_URL, processType } from '../../workitems/testFixtures';
import { server } from '../../../test/mocks/server';
import { serveBoards, type BoardsTestApi } from '../testApi';
import { cardMenuButton, cardOf, cardsIn, findBoard } from '../testBoard';
import {
  announcer,
  checkboxOf,
  counterOf,
  holdCreates,
  holdPatches,
  listOf,
} from '../testChecklist';
import {
  addTestCases,
  addTestcaseType,
  boardsDataset,
  catalogText,
  checklistDataset,
  ids,
  mskRecord,
  platformItem,
  problem,
  stateId,
} from '../testFixtures';
import { boardAddress, renderBoards } from '../testRender';

// feat-032 US-2: a child is closed and reopened from its checkbox (spec BR-C-3, -7, -8, -12;
// design § 7.2, § 8).

afterEach(() => {
  focusManager.setFocused(undefined);
});

function patches(api: BoardsTestApi) {
  return api.writes().filter((request) => request.method === 'PATCH');
}

/** US-1's board with the list of `kind` of MSK-US-2 open. */
async function openList(kind: 'task' | 'bug', typeName: string) {
  const api = serveBoards(checklistDataset());
  renderBoards(boardAddress());
  const board = await findBoard();
  await waitFor(() => expect(counterOf(board, 'MSK-US-9', 'task')).toBeInTheDocument());
  await userEvent.click(counterOf(board, 'MSK-US-2', kind));
  return { api, board, list: () => listOf(board, 'MSK-US-2', typeName) };
}

describe('US-2 Scenario: Ticking closes the child', () => {
  it('MSK-TASK-4: the row shows it checked and the counter «2/3» at once, announced at once; after the read it is «Закрыта» with only its state and «Изменён» changed', async () => {
    const { api, board, list } = await openList('task', 'Задача');
    const before = { ...api.item(ids.msk, 4) };
    const held = holdPatches();

    await userEvent.click(checkboxOf(list(), 'MSK-TASK-4'));

    // The write is still on the wire: what is shown is the optimistic row and counter.
    expect(checkboxOf(list(), 'MSK-TASK-4')).toBeChecked();
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('2/3');
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveAttribute(
      'aria-label',
      '2/3 — MSK-US-2, Задача: выполнено 2 из 3',
    );
    expect(announcer()).toHaveTextContent('MSK-TASK-4: статус «Закрыта».');
    expect(held.seen).toHaveLength(1);

    held.release();
    await waitFor(() =>
      expect(checkboxOf(list(), 'MSK-TASK-4')).not.toHaveAttribute('aria-disabled'),
    );

    const after = api.item(ids.msk, 4);
    expect(after.stateId).toBe(stateId('TASK', 'Закрыта'));
    expect(after.changedAt).not.toBe(before.changedAt);
    expect([after.title, after.assigneeId, after.areaId]).toEqual([
      before.title,
      before.assigneeId,
      before.areaId,
    ]);
    expect(patches(api).map((request) => request.body)).toEqual([
      { stateId: stateId('TASK', 'Закрыта') },
    ]);
    expect(checkboxOf(list(), 'MSK-TASK-4')).toBeChecked();
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('2/3');
  });
});

describe('US-2 Scenario: Unticking reopens the child in its initial state', () => {
  it('MSK-TASK-3 «Закрыта» unticked: the counter «0/3», «MSK-TASK-3: статус «Новая».» announced, and «Новая» stored', async () => {
    const { api, board, list } = await openList('task', 'Задача');

    await userEvent.click(checkboxOf(list(), 'MSK-TASK-3'));

    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('0/3');
    expect(announcer()).toHaveTextContent('MSK-TASK-3: статус «Новая».');
    await waitFor(() => expect(api.item(ids.msk, 3).stateId).toBe(stateId('TASK', 'Новая')));
    await waitFor(() =>
      expect(checkboxOf(list(), 'MSK-TASK-3')).not.toHaveAttribute('aria-disabled'),
    );
    expect(checkboxOf(list(), 'MSK-TASK-3')).not.toBeChecked();
  });
});

describe('US-2 Scenario: A bug is closed the same way', () => {
  it('MSK-BUG-8 «Разрешена» ticked ends in «Закрыта» and the bug counter reads «2/2»', async () => {
    const { api, board, list } = await openList('bug', 'Баг');

    await userEvent.click(checkboxOf(list(), 'MSK-BUG-8'));

    expect(counterOf(board, 'MSK-US-2', 'bug')).toHaveTextContent('2/2');
    await waitFor(() => expect(api.item(ids.msk, 8).stateId).toBe(stateId('BUG', 'Закрыта')));
    expect(announcer()).toHaveTextContent('MSK-BUG-8: статус «Закрыта».');
  });

  it('a «Разрешена» bug ticked and then unticked ends in «Новая»', async () => {
    const { api, list } = await openList('bug', 'Баг');

    await userEvent.click(checkboxOf(list(), 'MSK-BUG-8'));
    await waitFor(() =>
      expect(checkboxOf(list(), 'MSK-BUG-8')).not.toHaveAttribute('aria-disabled'),
    );
    await userEvent.click(checkboxOf(list(), 'MSK-BUG-8'));

    await waitFor(() => expect(api.item(ids.msk, 8).stateId).toBe(stateId('BUG', 'Новая')));
    expect(patches(api).map((request) => request.body)).toEqual([
      { stateId: stateId('BUG', 'Закрыта') },
      { stateId: stateId('BUG', 'Новая') },
    ]);
  });
});

describe('US-2 Scenario: A closed child that is also a card moves column at the read', () => {
  it('MSK-BUG-8’s own card stays in «Разрешена» at once and is in «Закрыта» after the read', async () => {
    const { board, list } = await openList('bug', 'Баг');
    const held = holdPatches();
    expect(cardsIn(board, 'Разрешена')).toContain('MSK-BUG-8');

    await userEvent.click(checkboxOf(list(), 'MSK-BUG-8'));

    expect(cardsIn(board, 'Разрешена')).toContain('MSK-BUG-8');
    expect(cardsIn(board, 'Закрыта')).not.toContain('MSK-BUG-8');
    held.release();
    await waitFor(() => expect(cardsIn(board, 'Закрыта')).toContain('MSK-BUG-8'));
    expect(cardsIn(board, 'Разрешена')).not.toContain('MSK-BUG-8');
  });
});

describe('US-2 Scenario: The checkbox can be used by keyboard', () => {
  it('Space on MSK-TASK-4’s checkbox ticks it, and focus stays on it after the read', async () => {
    const { api, list } = await openList('task', 'Задача');

    checkboxOf(list(), 'MSK-TASK-4').focus();
    await userEvent.keyboard(' ');

    await waitFor(() => expect(api.item(ids.msk, 4).stateId).toBe(stateId('TASK', 'Закрыта')));
    await waitFor(() =>
      expect(checkboxOf(list(), 'MSK-TASK-4')).not.toHaveAttribute('aria-disabled'),
    );
    expect(checkboxOf(list(), 'MSK-TASK-4')).toBeChecked();
    expect(checkboxOf(list(), 'MSK-TASK-4')).toHaveFocus();
  });
});

describe('US-2 Scenario: A refused write puts the row back', () => {
  it('shows the server’s text as an error notice, returns the checkbox and the counter, and reads the board again', async () => {
    const { api, board, list } = await openList('task', 'Задача');
    server.use(
      http.patch(WORK_ITEM_URL, () =>
        HttpResponse.json(problem('workitems.workitem.state_not_allowed'), { status: 409 }),
      ),
    );
    const reads = api.reads('/boards/stories').length;

    await userEvent.click(checkboxOf(list(), 'MSK-TASK-4'));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      catalogText('workitems.workitem.state_not_allowed'),
    );
    expect(checkboxOf(list(), 'MSK-TASK-4')).not.toBeChecked();
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('1/3');
    expect(api.reads('/boards/stories').length).toBeGreaterThan(reads);
    expect(api.item(ids.msk, 4).stateId).toBe(stateId('TASK', 'Новая'));
  });

  it('puts the row back when the server gives no answer, with «Не удалось сохранить статус. Попробуйте ещё раз.»', async () => {
    const { board, list } = await openList('task', 'Задача');
    server.use(http.patch(WORK_ITEM_URL, () => HttpResponse.error()));

    await userEvent.click(checkboxOf(list(), 'MSK-TASK-4'));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Не удалось сохранить статус. Попробуйте ещё раз.',
    );
    expect(checkboxOf(list(), 'MSK-TASK-4')).not.toBeChecked();
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('1/3');
  });
});

describe('US-2 Scenario: A child whose type has no Completed state cannot be ticked', () => {
  const task = processType('TASK');
  const saved = structuredClone(task.states);

  afterEach(() => {
    task.states.splice(0, task.states.length, ...saved);
  });

  it('its checkbox is disabled', async () => {
    task.states.splice(
      0,
      task.states.length,
      ...task.states.filter((state) => state.category.code !== 'Completed'),
    );
    const data = boardsDataset();
    const story = platformItem(2, 'US', 'Вход по паролю');
    data.items.push(story, platformItem(4, 'TASK', 'fe', { parentId: story.id }));
    const api = serveBoards(data);
    renderBoards(boardAddress());
    const board = await findBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));

    const checkbox = checkboxOf(listOf(board, 'MSK-US-2', 'Задача'), 'MSK-TASK-4');

    await waitFor(() => expect(checkbox).toBeDisabled());
    expect(patches(api)).toEqual([]);
  });
});

describe('US-2 Scenario: One write at a time', () => {
  it('while a tick is saved every checkbox, «Ещё действия», «Добавить задачу» is off and no card drags; the open field keeps its text and its Enter does nothing; then all work', async () => {
    const { api, board, list } = await openList('task', 'Задача');
    const addButton = within(cardOf(board, 'MSK-US-2')).getByRole('button', {
      name: 'Добавить задачу (MSK-US-2)',
    });
    await userEvent.click(addButton);
    const field = within(cardOf(board, 'MSK-US-2')).getByRole('textbox', {
      name: 'Заголовок: Задача (MSK-US-2)',
    });
    await userEvent.type(field, 'Проверить');
    const held = holdPatches();

    await userEvent.click(checkboxOf(list(), 'MSK-TASK-4'));

    for (const checkbox of within(list()).getAllByRole('checkbox')) {
      expect(checkbox).toHaveAttribute('aria-disabled', 'true');
    }
    expect(cardMenuButton('MSK-US-2')).toHaveAttribute('aria-disabled', 'true');
    expect(cardMenuButton('MSK-US-9')).toHaveAttribute('aria-disabled', 'true');
    expect(addButton).toHaveAttribute('aria-disabled', 'true');
    expect(cardOf(board, 'MSK-US-2')).toHaveAttribute('draggable', 'false');
    // A second tick while the first is saved is ignored.
    await userEvent.click(checkboxOf(list(), 'MSK-TASK-6'));
    expect(held.seen).toHaveLength(1);
    // The field keeps its text, and Enter creates nothing.
    field.focus();
    await userEvent.keyboard('{Enter}');
    expect(field).toHaveValue('Проверить');
    expect(api.writes().filter((request) => request.method === 'POST')).toEqual([]);

    held.release();
    await waitFor(() => expect(addButton).not.toHaveAttribute('aria-disabled'));
    expect(cardMenuButton('MSK-US-2')).not.toHaveAttribute('aria-disabled');
    expect(cardOf(board, 'MSK-US-2')).toHaveAttribute('draggable', 'true');
    expect(field).toHaveValue('Проверить');
    field.focus();
    await userEvent.keyboard('{Enter}');
    await waitFor(() =>
      expect(api.writes().filter((request) => request.method === 'POST')).toHaveLength(1),
    );
  });

  it('while a task is created every checkbox and «Добавить задачу» is off too', async () => {
    const { board, list } = await openList('task', 'Задача');
    const held = holdCreates();
    await userEvent.click(
      within(cardOf(board, 'MSK-US-2')).getByRole('button', { name: 'Добавить задачу (MSK-US-2)' }),
    );
    await userEvent.type(
      within(cardOf(board, 'MSK-US-2')).getByRole('textbox', {
        name: 'Заголовок: Задача (MSK-US-2)',
      }),
      'Новая работа{Enter}',
    );

    for (const checkbox of within(list()).getAllByRole('checkbox')) {
      expect(checkbox).toHaveAttribute('aria-disabled', 'true');
    }
    expect(cardMenuButton('MSK-US-2')).toHaveAttribute('aria-disabled', 'true');
    expect(cardOf(board, 'MSK-US-2')).toHaveAttribute('draggable', 'false');

    held.release();
    await waitFor(() =>
      expect(checkboxOf(list(), 'MSK-TASK-4')).not.toHaveAttribute('aria-disabled'),
    );
    expect(cardOf(board, 'MSK-US-2')).toHaveAttribute('draggable', 'true');
  });
});

describe('US-2 Scenario: The later of two writes decides', () => {
  it('Борис unticks MSK-TASK-3 on a board not read since Анна set it to «Активна»: it ends in «Новая», no notice', async () => {
    const { api, list } = await openList('task', 'Задача');
    // Анна's form save, as the server took it, which Борис's board has not read.
    mskRecord(api.data, 3).stateId = stateId('TASK', 'Активна');

    await userEvent.click(checkboxOf(list(), 'MSK-TASK-3'));

    await waitFor(() => expect(api.item(ids.msk, 3).stateId).toBe(stateId('TASK', 'Новая')));
    await waitFor(() =>
      expect(checkboxOf(list(), 'MSK-TASK-3')).not.toHaveAttribute('aria-disabled'),
    );
    expect(screen.queryByRole('alert')).toBeNull();
  });
});

describe('US-2 Scenario: A test case has no checkbox to use', () => {
  let cases: { restore(): void } | null = null;

  afterEach(() => {
    cases?.restore();
    cases = null;
  });

  it('the flask list of MSK-US-2 offers nothing to tick, and nothing is written', async () => {
    cases = addTestcaseType();
    const data = checklistDataset();
    addTestCases(data);
    const api = serveBoards(data);
    renderBoards(boardAddress());
    const board = await findBoard();

    await userEvent.click(counterOf(board, 'MSK-US-2', 'testcase'));

    const list = listOf(board, 'MSK-US-2', 'Тест-кейс');
    expect(within(list).queryAllByRole('checkbox')).toEqual([]);
    await userEvent.click(within(list).getByText('Письмо приходит'));
    expect(patches(api)).toEqual([]);
  });
});
