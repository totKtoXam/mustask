import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { serveBoards } from '../testApi';
import { cardOf, findBoard } from '../testBoard';
import { boardsDataset, platformItem, stateId } from '../testFixtures';
import { boardAddress, mskForm, renderBoards } from '../testRender';

// feat-029 US-1: a card's title opens the work item card as a dialog over the board (BR-C-1,
// BR-C-2) and the board reads again after the card saved (BR-C-21). The card inside is the
// kit's fields in jsdom, which are slow.

vi.setConfig({ testTimeout: 60_000 });

function dataset() {
  const data = boardsDataset();
  data.items.push(
    platformItem(5, 'BUG', 'Неверный счётчик', { stateId: stateId('BUG', 'Активна') }),
  );
  return data;
}

const location = () => screen.getByTestId('location');

async function titleLink(name = 'Неверный счётчик') {
  const board = await findBoard();
  return { board, link: within(cardOf(board, 'MSK-BUG-5')).getByRole('link', { name }) };
}

describe('feat-029 US-1: the board opens the card over itself', () => {
  it('opens the card on a plain click with the title’s address kept, and gives focus back to the title on Esc', async () => {
    serveBoards(dataset());
    renderBoards(boardAddress());
    const { link } = await titleLink();
    expect(link).toHaveAttribute('href', mskForm('MSK-BUG-5'));

    await userEvent.click(link);

    expect(await screen.findByRole('dialog', { name: 'Баг MSK-BUG-5' })).toBeVisible();
    expect(location()).toHaveTextContent(boardAddress());
    expect(location()).toHaveAttribute('data-search', '?workitem=MSK-BUG-5');
    expect(screen.getByRole('region', { name: 'Канбан', hidden: true })).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(location()).toHaveAttribute('data-search', '');
    expect(link).toHaveFocus();
  });

  it('leaves a modified click to the browser: no dialog, the address unchanged', async () => {
    serveBoards(dataset());
    renderBoards(boardAddress());
    const { link } = await titleLink();

    fireEvent.click(link, { ctrlKey: true });
    fireEvent.click(link, { shiftKey: true });
    fireEvent.click(link, { metaKey: true });

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(location()).toHaveAttribute('data-search', '');
    expect(location()).toHaveTextContent(boardAddress());
  });

  it('opens the card for an address with ?workitem= over the same board', async () => {
    serveBoards(dataset());
    renderBoards(`${boardAddress()}?workitem=MSK-BUG-5`);

    expect(await screen.findByRole('dialog', { name: 'Баг MSK-BUG-5' })).toBeVisible();
    expect(screen.getByRole('region', { name: 'Канбан', hidden: true })).toBeInTheDocument();
  });
});

describe('feat-029 BR-C-21: the board reads again after a save in the card', () => {
  it('shows the card’s new title once the dialog has saved it, the dialog staying open', async () => {
    const api = serveBoards(dataset());
    renderBoards(boardAddress());
    const { board, link } = await titleLink();
    await userEvent.click(link);
    await screen.findByRole('dialog', { name: 'Баг MSK-BUG-5' });
    const heading = await screen.findByRole('textbox', { name: 'Заголовок' });
    const reads = api.reads('/boards/stories').length;

    fireEvent.change(heading, { target: { value: 'Неверный счётчик заказов' } });
    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() =>
      expect(
        within(board).getByRole('link', { name: 'Неверный счётчик заказов', hidden: true }),
      ).toBeInTheDocument(),
    );
    expect(api.reads('/boards/stories').length).toBeGreaterThan(reads);
    expect(screen.getByRole('dialog', { name: 'Баг MSK-BUG-5' })).toBeVisible();
  });
});
