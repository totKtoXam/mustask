import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { serveBoards } from '../testApi';
import { cardOf, columnBox, findBoard } from '../testBoard';
import { counterOf, listOf } from '../testChecklist';
import { dataTransfer, fireDrag } from '../testDrag';
import { checklistDataset } from '../testFixtures';
import { boardAddress, renderBoards } from '../testRender';

// feat-032 review round 1, F-1: a memoised card draws again only when something it shows
// changes. The spy stands where `BoardColumns` puts `CardChecklists`, which takes `env`:
// were `env` new on every render of the board, every card would miss its memo and the spy
// would count one draw per card for each drag event (mod-006 US-5, design § 7.3).

const draws = vi.hoisted(() => ({ count: 0 }));

vi.mock('../components/CardChecklists', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../components/CardChecklists')>();
  const { memo, createElement } = await import('react');
  const Spy = memo(function CardChecklistsSpy(props: Parameters<typeof actual.CardChecklists>[0]) {
    draws.count += 1;
    return createElement(actual.CardChecklists, props);
  });
  return { ...actual, CardChecklists: Spy };
});

afterEach(() => {
  focusManager.setFocused(undefined);
});

describe('US-1 design § 7.3: the cards are drawn once', () => {
  it('with a list open, a dragstart and a column enter draw no card again', async () => {
    serveBoards(checklistDataset());
    renderBoards(boardAddress());
    const board = await findBoard();
    await waitFor(() => expect(counterOf(board, 'MSK-US-9', 'task')).toBeInTheDocument());
    await userEvent.click(counterOf(board, 'MSK-US-2', 'task'));
    expect(listOf(board, 'MSK-US-2', 'Задача')).toBeInTheDocument();
    // Let the board settle: the open list and the process read are drawn.
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 50));
    });

    const before = draws.count;
    const card = cardOf(board, 'MSK-US-2');
    const transfer = dataTransfer();
    await act(async () => {
      fireEvent.mouseDown(within(card).getByText('Активна'));
      fireDrag('dragStart', card, transfer);
    });
    expect(columnBox(board, 'Разрешена')).toHaveAttribute('data-drop-target', 'true');
    await act(async () => {
      fireDrag('dragEnter', columnBox(board, 'Разрешена'), transfer);
    });
    await act(async () => {
      fireDrag('dragEnd', card, dataTransfer());
    });

    expect(draws.count).toBe(before);
  });
});
