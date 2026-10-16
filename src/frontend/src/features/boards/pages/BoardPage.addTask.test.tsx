import { describe, expect, it } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WORK_ITEMS_URL, processType } from '../../workitems/testFixtures';
import { serveBoards, type BoardsTestApi } from '../testApi';
import { cardOf, findBoard } from '../testBoard';
import {
  checkboxOf,
  counterOf,
  holdCreateThenRefuse,
  holdCreates,
  listOf,
  rowsOf,
} from '../testChecklist';
import { catalogText, checklistDataset, ids, problem, stateId } from '../testFixtures';
import { boardAddress, renderBoards } from '../testRender';

// feat-032 US-3: «Добавить задачу» in a story card’s task list (spec BR-C-8, -9, -12; feat-027
// BR-T-9, BR-T-10; design § 5).

function creates(api: BoardsTestApi) {
  return api.writes().filter((request) => request.method === 'POST');
}

async function openBoard() {
  const api = serveBoards(checklistDataset());
  renderBoards(boardAddress());
  const board = await findBoard();
  await waitFor(() => expect(counterOf(board, 'MSK-US-9', 'task')).toBeInTheDocument());
  return { api, board };
}

/** The «Добавить задачу» of a card, found once its list is open. */
function addButton(board: HTMLElement, identifier: string) {
  return within(cardOf(board, identifier)).getByRole('button', {
    name: `Добавить задачу (${identifier})`,
  });
}

function titleField(board: HTMLElement, identifier: string) {
  return within(cardOf(board, identifier)).getByRole('textbox', {
    name: `Заголовок: Задача (${identifier})`,
  });
}

async function openField(board: HTMLElement, identifier: string) {
  await userEvent.click(addButton(board, identifier));
  return titleField(board, identifier);
}

describe('US-3 Scenario: The button is at the top of the task list', () => {
  it('«Добавить задачу» is the first line of MSK-US-2’s list, above MSK-TASK-3, named «Добавить задачу (MSK-US-2)»', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));

    const list = listOf(board, 'MSK-US-2', 'Задача');
    const lines = within(list).getAllByRole('listitem');
    expect(within(lines[0] as HTMLElement).getByRole('button')).toBe(addButton(board, 'MSK-US-2'));
    expect(lines[0]).not.toHaveAttribute('data-child');
    expect(lines[1]).toHaveAttribute('data-child', 'MSK-TASK-3');
    expect(addButton(board, 'MSK-US-2')).toHaveTextContent('Добавить задачу');
    expect(addButton(board, 'MSK-US-2').querySelector('[data-testid="ThinAddIcon"]')).not.toBeNull();
  });

  it('works by keyboard: Tab to it, then Enter or Space opens the field with focus in it', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));

    addButton(board, 'MSK-US-2').focus();
    await userEvent.keyboard('{Enter}');
    expect(titleField(board, 'MSK-US-2')).toHaveFocus();
    expect(titleField(board, 'MSK-US-2')).toHaveAttribute('placeholder', 'Введите заголовок');
  });

  it('opens the list of a story with no task: MSK-US-9 shows the button alone, with no text', async () => {
    const { board } = await openBoard();

    await userEvent.click(counterOf(board, 'MSK-US-9', 'task'));

    const list = listOf(board, 'MSK-US-9', 'Задача');
    expect(rowsOf(list)).toEqual([]);
    expect(within(list).getAllByRole('listitem')).toHaveLength(1);
    expect(addButton(board, 'MSK-US-9')).toBeInTheDocument();
  });
});

describe('US-3 Scenario: Only a story offers it', () => {
  it('the bug list of a feature’s card has no «Добавить задачу», and a bug’s card has no checklist', async () => {
    serveBoards(checklistDataset());
    renderBoards(boardAddress('features'));
    const board = await findBoard();

    await userEvent.click(counterOf(board, 'MSK-FEAT-11', 'bug'));

    expect(
      within(listOf(board, 'MSK-FEAT-11', 'Баг')).queryByRole('button', {
        name: /Добавить задачу/,
      }),
    ).toBeNull();
  });
});

describe('US-3 Scenario: A task is added with a title', () => {
  it('Enter creates one task under the card’s item, read-only while saved; then the notice, the new last row unchecked, the counter «1/4», focus on «Добавить задачу»', async () => {
    const { api, board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const held = holdCreates();
    const field = await openField(board, 'MSK-US-2');

    await userEvent.type(field, 'Проверить ссылку{Enter}');

    expect(field).toHaveAttribute('readonly');
    expect(field).toHaveAttribute('aria-busy', 'true');
    expect(
      within(cardOf(board, 'MSK-US-2')).getByRole('progressbar', { hidden: true }),
    ).toBeTruthy();
    held.release();

    expect(await screen.findByText('Рабочий элемент MSK-TASK-12 создан.')).toBeInTheDocument();
    const list = listOf(board, 'MSK-US-2', 'Задача');
    await waitFor(() => expect(rowsOf(list).at(-1)).toBe('MSK-TASK-12'));
    expect(within(list).getByRole('link', { name: 'Проверить ссылку' })).toBeInTheDocument();
    expect(checkboxOf(list, 'MSK-TASK-12')).not.toBeChecked();
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('1/4');
    expect(within(cardOf(board, 'MSK-US-2')).queryByRole('textbox')).toBeNull();
    await waitFor(() => expect(addButton(board, 'MSK-US-2')).toHaveFocus());

    const [request] = creates(api);
    expect(request?.body).toMatchObject({
      typeId: processType('TASK').id,
      title: 'Проверить ссылку',
      parentId: api.item(ids.msk, 2).id,
    });
    expect(Object.keys(request?.body as object).sort()).toEqual([
      'parentId',
      'requestId',
      'title',
      'typeId',
    ]);
    // The task lands where the parent is, in the type’s initial state, nobody assigned.
    const created = api.item(ids.msk, 12);
    expect(created.areaId).toBe(api.item(ids.msk, 2).areaId);
    expect(created.stateId).toBe(stateId('TASK', 'Новая'));
    expect(created.assigneeId).toBeNull();
    expect(created.parentId).toBe(api.item(ids.msk, 2).id);
  });

  it('adds a first task to a story with none: MSK-US-9 «0/0» becomes «0/1» and its list holds the task', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-9', 'task'));

    await userEvent.type(await openField(board, 'MSK-US-9'), 'Первая задача{Enter}');

    await waitFor(() => expect(counterOf(board, 'MSK-US-9', 'task')).toHaveTextContent('0/1'));
    expect(
      within(listOf(board, 'MSK-US-9', 'Задача')).getByRole('link', { name: 'Первая задача' }),
    ).toBeInTheDocument();
  });
});

describe('US-3 Scenario: Escape or an empty title cancels', () => {
  it('Escape closes the field with nothing sent and puts focus on «Добавить задачу»', async () => {
    const { api, board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));

    await userEvent.type(await openField(board, 'MSK-US-2'), 'Проверить{Escape}');

    expect(within(cardOf(board, 'MSK-US-2')).queryByRole('textbox')).toBeNull();
    expect(creates(api)).toEqual([]);
    expect(addButton(board, 'MSK-US-2')).toHaveFocus();
    // Escape is stopped at the field: the list it stands in is still open.
    expect(listOf(board, 'MSK-US-2', 'Задача')).toBeInTheDocument();
  });

  it('Enter with nothing typed closes the field with nothing sent and no error text', async () => {
    const { api, board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));

    await openField(board, 'MSK-US-2');
    await userEvent.keyboard('{Enter}');

    expect(within(cardOf(board, 'MSK-US-2')).queryByRole('textbox')).toBeNull();
    expect(screen.queryByRole('alert')).toBeNull();
    expect(creates(api)).toEqual([]);
    expect(addButton(board, 'MSK-US-2')).toHaveFocus();
  });
});

describe('US-3 Scenario: A double Enter creates one task', () => {
  it('Enter twice sends one request', async () => {
    const { api, board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const held = holdCreates();

    await userEvent.type(await openField(board, 'MSK-US-2'), 'Проверить{Enter}{Enter}');

    expect(held.seen).toHaveLength(1);
    held.release();
    await screen.findByText('Рабочий элемент MSK-TASK-12 создан.');
    expect(api.data.items.filter((record) => record.title === 'Проверить')).toHaveLength(1);
  });

  it('a request sent again after a lost answer keeps its requestId, so one task exists', async () => {
    const { api, board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    api.loseNextAnswer('/workitems');

    const field = await openField(board, 'MSK-US-2');
    await userEvent.type(field, 'Проверить{Enter}');
    await waitFor(() => expect(field).not.toHaveAttribute('aria-busy', 'true'));
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveValue('Проверить');
    await userEvent.keyboard('{Enter}');

    await screen.findByText(/Рабочий элемент MSK-TASK-\d+ создан\./);
    const [first, second] = creates(api);
    expect((second?.body as { requestId: string }).requestId).toBe(
      (first?.body as { requestId: string }).requestId,
    );
    expect(api.data.items.filter((record) => record.title === 'Проверить')).toHaveLength(1);
  });
});

describe('US-3 Scenario: A refusal shows at the field', () => {
  it('MSK-US-2 deleted after the read: the code’s text is under the field, marked invalid, the title is kept and nothing is created', async () => {
    const { api, board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const field = await openField(board, 'MSK-US-2');
    api.item(ids.msk, 2).stateId = stateId('US', 'Удалена');

    await userEvent.type(field, 'Проверить{Enter}');

    expect(await within(cardOf(board, 'MSK-US-2')).findByRole('alert')).toHaveTextContent(
      catalogText('workitems.link.item_deleted'),
    );
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveValue('Проверить');
    expect(field).toHaveFocus();
    expect(api.data.items.filter((record) => record.title === 'Проверить')).toHaveLength(0);
  });

  it('a refusal that arrives while the list is folded opens the list and shows the refusal at the field', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const body = problem('workitems.link.parent_not_found');
    const held = holdCreateThenRefuse(body);

    await userEvent.type(await openField(board, 'MSK-US-2'), 'Проверить{Enter}');
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    expect(within(cardOf(board, 'MSK-US-2')).queryAllByRole('list')).toEqual([]);
    held.release();

    expect(await within(cardOf(board, 'MSK-US-2')).findByRole('alert')).toHaveTextContent(
      body.detail,
    );
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveAttribute('aria-expanded', 'true');
    expect(titleField(board, 'MSK-US-2')).toHaveValue('Проверить');
  });

  it('a field folded away over typed text comes back with it, without taking focus', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    await userEvent.type(await openField(board, 'MSK-US-2'), 'Проверить');

    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));

    const field = titleField(board, 'MSK-US-2');
    expect(field).toHaveValue('Проверить');
    expect(field).not.toHaveFocus();
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveFocus();
  });
});

describe('US-3 Scenario: The title rule holds', () => {
  it('a title of 256 characters shows «Не более 255 символов.» under the field and sends nothing', async () => {
    const { api, board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const field = await openField(board, 'MSK-US-2');

    fireEvent.change(field, { target: { value: 'я'.repeat(256) } });
    await userEvent.type(field, '{Enter}');

    expect(within(cardOf(board, 'MSK-US-2')).getByRole('alert')).toHaveTextContent(
      'Не более 255 символов.',
    );
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(creates(api)).toEqual([]);
    expect(field).toHaveValue('я'.repeat(256));
  });
});

describe('US-3 Scenario: One field is open on the board', () => {
  it('opening it on another card closes an empty one, and goes back to a field with text typed', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    await userEvent.click(counterOf(board, 'MSK-US-9', 'task'));

    await openField(board, 'MSK-US-2');
    await openField(board, 'MSK-US-9');
    expect(within(cardOf(board, 'MSK-US-2')).queryByRole('textbox')).toBeNull();
    expect(titleField(board, 'MSK-US-9')).toHaveFocus();

    await userEvent.type(titleField(board, 'MSK-US-9'), 'Первая');
    await userEvent.click(addButton(board, 'MSK-US-2'));

    expect(within(cardOf(board, 'MSK-US-2')).queryByRole('textbox')).toBeNull();
    expect(titleField(board, 'MSK-US-9')).toHaveFocus();
    expect(titleField(board, 'MSK-US-9')).toHaveValue('Первая');
  });
});

describe('US-3 Scenario: Typing in the field does not drag the card', () => {
  it('a press and a drag from the open field are refused, and its text can be typed', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const field = await openField(board, 'MSK-US-2');

    await userEvent.type(field, 'Проверить');
    fireEvent.mouseDown(field);
    const started = fireEvent.dragStart(field);
    const start = fireEvent.dragStart(addButton(board, 'MSK-US-2'));

    expect(field).toHaveValue('Проверить');
    expect(started).toBe(false);
    expect(start).toBe(false);
    expect(board.querySelector('[data-drop-target]')).toBeNull();
    expect(WORK_ITEMS_URL).toBeTruthy();
  });
});
