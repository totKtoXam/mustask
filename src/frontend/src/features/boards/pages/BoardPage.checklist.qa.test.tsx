import { afterEach, describe, expect, it } from 'vitest';
import { waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { serveBoards } from '../testApi';
import { cardOf, findBoard } from '../testBoard';
import { checkboxOf, counterOf, listOf } from '../testChecklist';
import { checklistDataset, ids, stateId } from '../testFixtures';
import { boardAddress, renderBoards } from '../testRender';

// feat-032 QA additions: the keyboard path through an open card (design § 8) and the
// independence of two open lists when one of them is written to (BR-C-4, BR-C-7).

afterEach(() => {
  focusManager.setFocused(undefined);
});

async function openBoard() {
  const api = serveBoards(checklistDataset());
  renderBoards(boardAddress());
  const board = await findBoard();
  await waitFor(() => expect(counterOf(board, 'MSK-US-9', 'task')).toBeInTheDocument());
  return { api, board };
}

/** The accessible label, else the text, of a focused control. */
function nameOf(element: Element): string {
  return element.getAttribute('aria-label') ?? element.textContent ?? '';
}

describe('US-1 design § 8: the Tab order inside a card', () => {
  it('runs counters, then the open task list’s «Добавить задачу», each row’s checkbox and title link, then the chevron; avatars are no stops', async () => {
    const { board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    counterOf(board, 'MSK-US-2', 'task').focus();

    const stops: string[] = [nameOf(document.activeElement as Element)];
    // The whole task list, until focus leaves the card (the bug counter is before it, so Tab runs forward from
    // the task counter: bug counter, then the list).
    for (let step = 0; step < 12; step += 1) {
      await userEvent.tab();
      const focused = document.activeElement as Element;
      if (!cardOf(board, 'MSK-US-2').contains(focused)) {
        break;
      }
      stops.push(nameOf(focused));
    }

    expect(stops).toEqual([
      '1/3 — MSK-US-2, Задача: выполнено 1 из 3',
      '1/2 — MSK-US-2, Баг: выполнено 1 из 2',
      'Добавить задачу (MSK-US-2)',
      'MSK-TASK-3: выполнено',
      'be',
      'MSK-TASK-4: выполнено',
      'fe',
      'MSK-TASK-6: выполнено',
      'qa',
      'Свернуть список: Задача (MSK-US-2)',
    ]);
  });
});

describe('US-1 / US-2: two open lists are independent when one is written to', () => {
  it('ticking a bug moves the bug counter only; the task list, its counter and its boxes stay as they were', async () => {
    const { api, board } = await openBoard();
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    await userEvent.click(counterOf(board, 'MSK-US-2', 'bug'));

    await userEvent.click(checkboxOf(listOf(board, 'MSK-US-2', 'Баг'), 'MSK-BUG-8'));

    await waitFor(() => expect(api.item(ids.msk, 8).stateId).toBe(stateId('BUG', 'Закрыта')));
    await waitFor(() => expect(counterOf(board, 'MSK-US-2', 'bug')).toHaveTextContent('2/2'));
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveTextContent('1/3');
    expect(counterOf(board, 'MSK-US-2', 'task')).toHaveAttribute('aria-expanded', 'true');
    expect(counterOf(board, 'MSK-US-2', 'bug')).toHaveAttribute('aria-expanded', 'true');
    const tasks = listOf(board, 'MSK-US-2', 'Задача');
    expect(checkboxOf(tasks, 'MSK-TASK-3')).toBeChecked();
    expect(checkboxOf(tasks, 'MSK-TASK-4')).not.toBeChecked();
    expect(checkboxOf(tasks, 'MSK-TASK-6')).not.toBeChecked();
  });
});
