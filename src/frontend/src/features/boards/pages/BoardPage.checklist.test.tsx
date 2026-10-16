import { afterEach, describe, expect, it } from 'vitest';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { serveBoards } from '../testApi';
import { cardMenuButton, cardOf, cardsIn, columnBox, columnNames, findBoard } from '../testBoard';
import { checkboxOf, counterOf, countersOfCard, listOf, rowOf, rowsOf } from '../testChecklist';
import { dataTransfer, fireDrag } from '../testDrag';
import {
  addTestCases,
  addTestcaseType,
  boardsDataset,
  checklistDataset,
  ids,
  mskRecord,
  platformItem,
  stateId,
} from '../testFixtures';
import { boardAddress, mskForm, renderBoards } from '../testRender';

// feat-032 US-1: a card's tasks, bugs and test cases as collapsible checklists, with who is
// assigned (spec BR-C-1..-6, -10..-12; design § 3, § 4, § 8, § 9).

let testcases: { restore(): void } | null = null;

afterEach(() => {
  testcases?.restore();
  testcases = null;
  focusManager.setFocused(undefined);
});

/** US-1's board, with the fixture test cases when `withTestcases`. */
async function openBoard(options: { withTestcases?: boolean; level?: string } = {}) {
  const data = checklistDataset();
  if (options.withTestcases) {
    testcases = addTestcaseType();
    addTestCases(data);
  }
  const api = serveBoards(data);
  renderBoards(boardAddress(options.level));
  const board = await findBoard();
  if (options.level === undefined) {
    // The «0/0» of a story with no child waits for the process: its counter is the signal.
    await waitFor(() => expect(countersOfCard(board, 'MSK-US-9')).toHaveLength(1));
  }
  return { api, board, data };
}

describe('US-1 Scenario: A card shows its counters under its assignee, lists collapsed', () => {
  it('MSK-US-2 shows the task counter «1/3» and the bug counter «1/2», named as D-5 asks, and no list; MSK-TASK-7 in «Удалена» is in no counter', async () => {
    const { board } = await openBoard();

    const counters = countersOfCard(board, 'MSK-US-2');
    expect(counters.map((counter) => counter.textContent)).toEqual(['1/3', '1/2']);
    expect(counters.map((counter) => counter.getAttribute('aria-label'))).toEqual([
      '1/3 — MSK-US-2, Задача: выполнено 1 из 3',
      '1/2 — MSK-US-2, Баг: выполнено 1 из 2',
    ]);
    expect(counters.map((counter) => counter.getAttribute('aria-expanded'))).toEqual([
      'false',
      'false',
    ]);
    expect(within(cardOf(board, 'MSK-US-2')).queryAllByRole('list')).toEqual([]);
    expect(within(cardOf(board, 'MSK-US-2')).queryByText('release')).toBeNull();
    // Under the assignee: the group is the card’s last block.
    const card = cardOf(board, 'MSK-US-2');
    expect(card.textContent?.indexOf('Анна Смирнова')).toBeLessThan(
      card.textContent?.indexOf('1/3') ?? -1,
    );
  });

  it('draws each counter with its kind’s icon — the clipboard for tasks, the bug for bugs', async () => {
    const { board } = await openBoard();

    expect(counterOf(board, 'MSK-US-2', 'task').querySelector('svg')).toHaveAttribute(
      'data-testid',
      'AssignmentTurnedInIcon',
    );
    expect(counterOf(board, 'MSK-US-2', 'bug').querySelector('svg')).toHaveAttribute(
      'data-testid',
      'BugReportIcon',
    );
  });

  it('counts a «Разрешена» bug in the total and not in the done count', async () => {
    const { board } = await openBoard();

    // MSK-BUG-5 «Закрыта» is done; MSK-BUG-8 «Разрешена» is not.
    expect(counterOf(board, 'MSK-US-2', 'bug')).toHaveTextContent('1/2');
    await userEvent.click(counterOf(board, 'MSK-US-2', 'bug'));
    const list = listOf(board, 'MSK-US-2', 'Баг');
    expect(checkboxOf(list, 'MSK-BUG-5')).toBeChecked();
    expect(checkboxOf(list, 'MSK-BUG-8')).not.toBeChecked();
  });
});

describe('US-1 Scenario: Test cases are a count only, whatever their state', () => {
  it('with the fixture process the card also shows the flask with «2», named «2 — MSK-US-2, Тест-кейс: 2», after the bug counter; «Закрыта» is counted', async () => {
    const { board } = await openBoard({ withTestcases: true });

    const counters = countersOfCard(board, 'MSK-US-2');
    expect(counters.map((counter) => counter.textContent)).toEqual(['1/3', '1/2', '2']);
    const flask = counterOf(board, 'MSK-US-2', 'testcase');
    expect(flask).toHaveAttribute('aria-label', '2 — MSK-US-2, Тест-кейс: 2');
    expect(flask.querySelector('svg')).toHaveAttribute('data-testid', 'ScienceIcon');
  });

  it('without a test-case type in the process there is no flask counter on any card', async () => {
    const { board } = await openBoard();

    expect(board.querySelector('[data-counter="testcase"]')).toBeNull();
    expect(within(board).queryByTestId('ScienceIcon')).toBeNull();
  });

  it('a test case’s row has the flask, an avatar and the title, and no checkbox', async () => {
    const { board } = await openBoard({ withTestcases: true });

    await userEvent.click(counterOf(board, 'MSK-US-2', 'testcase'));

    const list = listOf(board, 'MSK-US-2', 'Тест-кейс');
    expect(rowsOf(list)).toEqual(['MSK-TC-10', 'MSK-TC-12']);
    const row = rowOf(list, 'MSK-TC-10');
    expect(within(row).queryByRole('checkbox')).toBeNull();
    expect(row.querySelector('[data-testid="ScienceIcon"]')).not.toBeNull();
    expect(within(row).getByRole('img', { name: 'Анна Смирнова' })).toBeInTheDocument();
    expect(within(row).getByRole('link', { name: 'Письмо приходит' })).toBeInTheDocument();
  });
});

describe('US-1 Scenario: A story with no children still has its task counter', () => {
  it('MSK-US-9 shows «0/0» and no other counter; a feature with no children shows no row at all', async () => {
    serveBoards(checklistDataset());
    renderBoards(boardAddress());
    const board = await findBoard();

    await waitFor(() => expect(countersOfCard(board, 'MSK-US-9')).toHaveLength(1));
    const [counter] = countersOfCard(board, 'MSK-US-9');
    expect(counter).toHaveTextContent('0/0');
    expect(counter).toHaveAttribute('aria-label', '0/0 — MSK-US-9, Задача: выполнено 0 из 0');
  });

  it('a card of a type that offers no task, with no children, shows no row of counters', async () => {
    const data = checklistDataset();
    data.items.push(platformItem(20, 'FEAT', 'Вход по коду'));
    serveBoards(data);
    renderBoards(boardAddress('features'));
    const board = await findBoard();

    expect(cardOf(board, 'MSK-FEAT-20')).toBeInTheDocument();
    expect(
      within(cardOf(board, 'MSK-FEAT-20')).queryByRole('group', {
        name: /Вложенные элементы/,
      }),
    ).toBeNull();
  });
});

describe('US-1 Scenario: A counter opens its own list and a second press closes it', () => {
  it('opens MSK-TASK-3, MSK-TASK-4, MSK-TASK-6 in that order, the counter is expanded and the card stays in its column; a second press closes it', async () => {
    const { board } = await openBoard();
    const counter = counterOf(board, 'MSK-US-2', 'task');

    await userEvent.click(counter);

    expect(rowsOf(listOf(board, 'MSK-US-2', 'Задача'))).toEqual([
      'MSK-TASK-3',
      'MSK-TASK-4',
      'MSK-TASK-6',
    ]);
    expect(counter).toHaveAttribute('aria-expanded', 'true');
    expect(counter).toHaveAttribute('aria-controls');
    expect(cardsIn(board, 'Активна')).toContain('MSK-US-2');

    await userEvent.click(counter);

    expect(within(cardOf(board, 'MSK-US-2')).queryAllByRole('list')).toEqual([]);
    expect(counter).toHaveAttribute('aria-expanded', 'false');
  });

  it('works by keyboard: Enter opens the list and Space closes it', async () => {
    const { board } = await openBoard();
    const counter = counterOf(board, 'MSK-US-2', 'task');

    counter.focus();
    await userEvent.keyboard('{Enter}');
    expect(counter).toHaveAttribute('aria-expanded', 'true');
    expect(listOf(board, 'MSK-US-2', 'Задача')).toBeInTheDocument();

    await userEvent.keyboard(' ');
    expect(counter).toHaveAttribute('aria-expanded', 'false');
    expect(within(cardOf(board, 'MSK-US-2')).queryAllByRole('list')).toEqual([]);
    expect(counter).toHaveFocus();
  });

  it('keeps the lists independent and in kind order, whichever was opened first', async () => {
    const { board } = await openBoard();

    await userEvent.click(counterOf(board, 'MSK-US-2', 'bug'));
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));

    const tasks = listOf(board, 'MSK-US-2', 'Задача');
    const bugs = listOf(board, 'MSK-US-2', 'Баг');
    expect(tasks.compareDocumentPosition(bugs) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    // One does not close the other.
    await userEvent.click(counterOf(board, 'MSK-US-2', 'bug'));
    expect(listOf(board, 'MSK-US-2', 'Задача')).toBeInTheDocument();
    expect(within(cardOf(board, 'MSK-US-2')).queryByRole('list', { name: /Баг/ })).toBeNull();
  });

  it('sends no request when a list opens (BR-C-10)', async () => {
    const { api, board } = await openBoard();
    const before = api.requests.length;

    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    await userEvent.click(counterOf(board, 'MSK-US-2', 'bug'));

    expect(api.requests).toHaveLength(before);
  });

  it('draws the open counter selected and does not announce it as pressed', async () => {
    const { board } = await openBoard();
    const counter = counterOf(board, 'MSK-US-2', 'task');

    await userEvent.click(counter);

    expect(counter).not.toHaveAttribute('aria-pressed');
    expect(counter).toHaveAttribute('aria-expanded', 'true');
  });
});

describe('US-1 Scenario: The rows', () => {
  it('lists the bugs in the order of their numbers — MSK-BUG-5 before MSK-BUG-8', async () => {
    const { board } = await openBoard();

    await userEvent.click(counterOf(board, 'MSK-US-2', 'bug'));

    expect(rowsOf(listOf(board, 'MSK-US-2', 'Баг'))).toEqual(['MSK-BUG-5', 'MSK-BUG-8']);
  });

  it('shows a checkbox, the task icon, an avatar and the title as a link; MSK-TASK-3 is checked, MSK-TASK-4 and MSK-TASK-6 are not', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const list = listOf(board, 'MSK-US-2', 'Задача');

    const row = rowOf(list, 'MSK-TASK-3');
    expect(within(row).getByRole('checkbox', { name: 'MSK-TASK-3: выполнено' })).toBeChecked();
    expect(row.querySelector('[data-testid="AssignmentTurnedInIcon"]')).not.toBeNull();
    expect(within(row).getByRole('link', { name: 'be' })).toBeInTheDocument();
    expect(checkboxOf(list, 'MSK-TASK-4')).not.toBeChecked();
    expect(checkboxOf(list, 'MSK-TASK-6')).not.toBeChecked();
  });

  it('names the avatar by the person: «АС» is «Анна Смирнова», nobody is «Не назначен» with the neutral person icon', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const list = listOf(board, 'MSK-US-2', 'Задача');

    const known = within(rowOf(list, 'MSK-TASK-3')).getByRole('img', { name: 'Анна Смирнова' });
    expect(known).toHaveTextContent('АС');
    const nobody = within(rowOf(list, 'MSK-TASK-4')).getByRole('img', { name: 'Не назначен' });
    expect(nobody.querySelector('[data-testid="ThinPersonIcon"]')).not.toBeNull();
    expect(nobody).toHaveTextContent('');
    // The avatar is not a tab stop.
    expect(known).not.toHaveAttribute('tabindex');
  });

  it('shows the neutral avatar named «Неизвестный пользователь» for an assignee the directory does not know', async () => {
    const data = checklistDataset();
    mskRecord(data, 6).assigneeId = ids.ghost;
    serveBoards(data);
    renderBoards(boardAddress());
    const board = await findBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));

    const avatar = within(rowOf(listOf(board, 'MSK-US-2', 'Задача'), 'MSK-TASK-6')).getByRole(
      'img',
      { name: 'Неизвестный пользователь' },
    );
    expect(avatar.querySelector('[data-testid="ThinPersonIcon"]')).not.toBeNull();
  });

  it('shows the assignee on the card: «Анна Смирнова» on MSK-US-2, «Не назначен» on MSK-US-9', async () => {
    const { board } = await openBoard();

    expect(within(cardOf(board, 'MSK-US-2')).getByText('Анна Смирнова')).toBeInTheDocument();
    expect(within(cardOf(board, 'MSK-US-9')).getByText('Не назначен')).toBeInTheDocument();
  });

  it('opens the child from the row’s title the way the card’s own title opens an item', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const list = listOf(board, 'MSK-US-2', 'Задача');

    const link = within(rowOf(list, 'MSK-TASK-3')).getByRole('link', { name: 'be' });

    expect(link).toHaveAttribute('href', mskForm('MSK-TASK-3'));
    expect(link).toHaveAttribute('title', 'MSK-TASK-3: be');
    expect(
      within(cardOf(board, 'MSK-US-2')).getByRole('link', { name: /Повторная/ }),
    ).toHaveAttribute('href', mskForm('MSK-US-2'));
    await userEvent.click(link);
    expect(await screen.findByText('Другая страница')).toBeInTheDocument();
  });
});

describe('US-1 Scenario: The chevron closes a list', () => {
  it('«Свернуть список: Задача (MSK-US-2)» closes the task list and puts focus on the task counter', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));

    await userEvent.click(
      within(cardOf(board, 'MSK-US-2')).getByRole('button', {
        name: 'Свернуть список: Задача (MSK-US-2)',
      }),
    );

    expect(within(cardOf(board, 'MSK-US-2')).queryAllByRole('list')).toEqual([]);
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveFocus();
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveAttribute('aria-expanded', 'false');
  });
});

describe('US-1 Scenario: What was opened stays opened across reads, not across visits', () => {
  it('keeps the task list of MSK-US-2 open when the card moves to another column and the board is read again', async () => {
    const { api, board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));

    await userEvent.click(cardMenuButton('MSK-US-2'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Разрешена' }));

    await waitFor(() => expect(cardsIn(board, 'Разрешена')).toContain('MSK-US-2'));
    await waitFor(() => expect(cardMenuButton('MSK-US-2')).not.toHaveAttribute('aria-disabled'));
    expect(api.reads('/boards/stories').length).toBeGreaterThan(1);
    expect(rowsOf(listOf(board, 'MSK-US-2', 'Задача'))).toEqual([
      'MSK-TASK-3',
      'MSK-TASK-4',
      'MSK-TASK-6',
    ]);
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveAttribute('aria-expanded', 'true');
  });

  it('starts every list collapsed on a new visit', async () => {
    const data = checklistDataset();
    serveBoards(data);
    const first = renderBoards(boardAddress());
    const board = await findBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    expect(listOf(board, 'MSK-US-2', 'Задача')).toBeInTheDocument();
    first.unmount();

    renderBoards(boardAddress());
    const again = await findBoard();

    expect(within(cardOf(again, 'MSK-US-2')).queryAllByRole('list')).toEqual([]);
    expect(counterOf(again, 'MSK-US-2', 'task')).toHaveAttribute('aria-expanded', 'false');
  });
});

describe('US-1 Scenario: Controls inside the card do not start a card drag (BR-C-12)', () => {
  /** A drag that starts on `element` after a press on it; `true`: the card’s drag began. */
  async function dragFrom(element: Element): Promise<boolean> {
    const transfer = dataTransfer();
    fireEvent.mouseDown(element);
    let started = false;
    await act(async () => {
      started = fireDrag('dragStart', element, transfer);
    });
    return started;
  }

  async function endDrag(card: Element) {
    await act(async () => fireDrag('dragEnd', card, dataTransfer()));
  }

  it('a press and drag from a counter, a checkbox, a row’s link, an avatar or a chevron is refused and the card drags nowhere', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const card = cardOf(board, 'MSK-US-2');
    const list = listOf(board, 'MSK-US-2', 'Задача');
    const controls = [
      counterOf(board, 'MSK-US-2', 'bug'),
      checkboxOf(list, 'MSK-TASK-4'),
      within(rowOf(list, 'MSK-TASK-4')).getByRole('link', { name: 'fe' }),
      within(rowOf(list, 'MSK-TASK-3')).getByRole('img', { name: 'Анна Смирнова' }),
      within(card).getByRole('button', { name: 'Свернуть список: Задача (MSK-US-2)' }),
    ];

    for (const control of controls) {
      expect(await dragFrom(control)).toBe(false);
      expect(board.querySelector('[data-drop-target]')).toBeNull();
      await endDrag(card);
    }
  });

  it('a drag from the rest of the card’s surface starts as in mod-006 US-5, after a press elsewhere in the card', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const card = cardOf(board, 'MSK-US-2');
    // A press on a control first: the next press, on the state, clears the flag.
    expect(await dragFrom(counterOf(board, 'MSK-US-2', 'bug'))).toBe(false);
    await endDrag(card);

    expect(await dragFrom(within(card).getByText('Активна'))).toBe(true);

    expect(columnBox(board, 'Разрешена')).toHaveAttribute('data-drop-target', 'true');
    await endDrag(card);
  });
});

describe('US-1 Scenario: Only task, bug and test-case children are listed', () => {
  it('the «Фичи» board: MSK-FEAT-11 has no counter for its story MSK-US-2, and a bug counter «0/1» for MSK-BUG-1', async () => {
    const { board } = await openBoard({ level: 'features' });

    const counters = countersOfCard(board, 'MSK-FEAT-11');
    expect(counters.map((counter) => counter.textContent)).toEqual(['0/1']);
    expect(counters[0]).toHaveAttribute('data-counter', 'bug');
    expect(counters[0]).toHaveAttribute('aria-label', '0/1 — MSK-FEAT-11, Баг: выполнено 0 из 1');
    await userEvent.click(counters[0] as HTMLElement);
    expect(rowsOf(listOf(board, 'MSK-FEAT-11', 'Баг'))).toEqual(['MSK-BUG-1']);
  });

  it('a child of another team’s area is in its parent’s counter and list', async () => {
    const data = checklistDataset();
    mskRecord(data, 4).areaId = ids.mskRoot;
    serveBoards(data);
    renderBoards(boardAddress());
    const board = await findBoard();

    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('1/3');
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    expect(rowsOf(listOf(board, 'MSK-US-2', 'Задача'))).toContain('MSK-TASK-4');
  });

  it('a child bug is still a card of its own on the «Истории» board', async () => {
    const { board } = await openBoard();

    expect(cardsIn(board, 'Закрыта')).toContain('MSK-BUG-5');
    expect(cardsIn(board, 'Разрешена')).toContain('MSK-BUG-8');
    await userEvent.click(counterOf(board, 'MSK-US-2', 'bug'));
    expect(rowsOf(listOf(board, 'MSK-US-2', 'Баг'))).toEqual(['MSK-BUG-5', 'MSK-BUG-8']);
  });
});

describe('US-1 Scenario: The checklists come with the board’s answer', () => {
  it('a card with 200 children opens its list with all rows drawn and no request', async () => {
    const data = boardsDataset();
    const story = platformItem(2, 'US', 'Большая история');
    data.items.push(story);
    for (let number = 3; number < 203; number += 1) {
      data.items.push(platformItem(number, 'TASK', `Задача ${number}`, { parentId: story.id }));
    }
    const api = serveBoards(data);
    renderBoards(boardAddress());
    const board = await findBoard();
    const before = api.requests.length;

    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));

    expect(rowsOf(listOf(board, 'MSK-US-2', 'Задача'))).toHaveLength(200);
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('0/200');
    expect(api.requests).toHaveLength(before);
  });
});

describe('US-1 Scenario: The board’s other behaviour is unchanged', () => {
  it('keeps the columns, their counts, «Ещё действия: <ID>» and the card’s title link', async () => {
    const { board } = await openBoard();

    expect(columnNames(board)).toEqual([
      'Новая, рабочих элементов: 1',
      'Активна, рабочих элементов: 2',
      'Разрешена, рабочих элементов: 1',
      'Закрыта, рабочих элементов: 1',
    ]);
    expect(cardMenuButton('MSK-US-2')).toBeInTheDocument();
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-9']);
  });
});

describe('US-1 design § 7.2: a re-read while a list is open', () => {
  it('replaces the rows in place by the child’s id, so focus on a checkbox stays', async () => {
    const { api, board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    const list = listOf(board, 'MSK-US-2', 'Задача');
    const checkbox = checkboxOf(list, 'MSK-TASK-4');
    checkbox.focus();
    const reads = api.reads('/boards/stories').length;

    await act(async () => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    await waitFor(() => expect(api.reads('/boards/stories').length).toBeGreaterThan(reads));
    expect(checkboxOf(listOf(board, 'MSK-US-2', 'Задача'), 'MSK-TASK-4')).toBe(checkbox);
    expect(checkbox).toHaveFocus();
  });

  it('puts focus on the list’s counter when the focused row has left the read', async () => {
    const { api, board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    checkboxOf(listOf(board, 'MSK-US-2', 'Задача'), 'MSK-TASK-6').focus();
    // MSK-TASK-6 is removed by someone else; the board is read again.
    api.item(ids.msk, 6).stateId = stateId('TASK', 'Удалена');

    await act(async () => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    await waitFor(() => expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('1/2'));
    expect(rowsOf(listOf(board, 'MSK-US-2', 'Задача'))).toEqual(['MSK-TASK-3', 'MSK-TASK-4']);
    await waitFor(() => expect(counterOf(board, 'MSK-US-2', 'task')).toHaveFocus());
  });
});
