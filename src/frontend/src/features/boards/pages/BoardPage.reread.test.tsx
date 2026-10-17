import { afterEach, describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { focusManager } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { serveBoards } from '../testApi';
import { cardsIn, findBoard } from '../testBoard';
import { BOARD_URL, boardsDataset, platformItem } from '../testFixtures';
import { boardAddress, renderBoards } from '../testRender';

// feat-032 design § 7.1 ("a failed re-read keeps the board and its open lists, with the banner
// above"), which the project's board and a team's share (feat-039 BR-PB-8): a re-read that
// fails while the columns are shown leaves them as they were, with the error above them (the
// backlog's rule since feat-027 D-6); a retry that succeeds takes the error away.

afterEach(() => {
  focusManager.setFocused(undefined);
});

describe('A team’s board: a failed re-read keeps the board', () => {
  it('shows the error above the columns and their cards; the retry takes it away', async () => {
    const data = boardsDataset();
    data.items.push(platformItem(2, 'US', 'Вход по паролю'), platformItem(3, 'US', 'Выход'));
    serveBoards(data);
    renderBoards(boardAddress());
    const board = await findBoard();
    server.use(
      http.get(
        BOARD_URL,
        () =>
          HttpResponse.json(
            { status: 500, title: 'Ошибка', detail: 'Сервис не ответил.' },
            { status: 500 },
          ),
        { once: true },
      ),
    );

    focusManager.setFocused(false);
    focusManager.setFocused(true);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Сервис не ответил.');
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-2', 'MSK-US-3']);

    await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));

    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
    expect(cardsIn(board, 'Новая')).toEqual(['MSK-US-2', 'MSK-US-3']);
  });
});
