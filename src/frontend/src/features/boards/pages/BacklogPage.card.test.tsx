import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { serveBoards } from '../testApi';
import { findBacklogTable, rowsOf } from '../testBacklog';
import { boardsDataset, platformItem, stateId } from '../testFixtures';
import { backlogAddress, mskForm, renderBoards } from '../testRender';

// feat-029 US-1: a row's title opens the work item card as a dialog over the backlog (BR-C-1,
// BR-C-2) and the backlog reads again after the card saved (BR-C-21). The card inside is the
// kit's fields in jsdom, which are slow.

vi.setConfig({ testTimeout: 60_000 });

function dataset() {
  const data = boardsDataset();
  data.items.push(
    platformItem(4, 'BUG', 'Падение при входе', { stateId: stateId('BUG', 'Активна') }),
  );
  return data;
}

const location = () => screen.getByTestId('location');

async function titleLink(name = 'Падение при входе') {
  const table = await findBacklogTable();
  const [row] = rowsOf(table);
  if (!row) throw new Error('No row.');
  return { table, link: within(row).getByRole('link', { name }) };
}

describe('feat-029 US-1: the backlog opens the card over itself', () => {
  it('opens the card on a plain click with the title’s address kept, and gives focus back to the title on Esc', async () => {
    serveBoards(dataset());
    renderBoards(backlogAddress());
    const { link } = await titleLink();
    expect(link).toHaveAttribute('href', mskForm('MSK-BUG-4'));

    await userEvent.click(link);

    expect(await screen.findByRole('dialog', { name: 'Баг MSK-BUG-4' })).toBeVisible();
    expect(location()).toHaveTextContent(backlogAddress());
    expect(location()).toHaveAttribute('data-search', '?workitem=MSK-BUG-4');
    expect(screen.getByRole('table', { name: 'Бэклог', hidden: true })).toBeInTheDocument();

    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(location()).toHaveAttribute('data-search', '');
    expect(link).toHaveFocus();
  });

  it('leaves a modified click to the browser: no dialog, the address unchanged', async () => {
    serveBoards(dataset());
    renderBoards(backlogAddress());
    const { link } = await titleLink();

    fireEvent.click(link, { ctrlKey: true });
    fireEvent.click(link, { shiftKey: true });
    fireEvent.click(link, { metaKey: true });

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(location()).toHaveAttribute('data-search', '');
    expect(location()).toHaveTextContent(backlogAddress());
  });

  it('opens the card for an address with ?workitem= over the same backlog', async () => {
    serveBoards(dataset());
    renderBoards(`${backlogAddress()}?workitem=MSK-BUG-4`);

    expect(await screen.findByRole('dialog', { name: 'Баг MSK-BUG-4' })).toBeVisible();
    expect(screen.getByRole('table', { name: 'Бэклог', hidden: true })).toBeInTheDocument();
  });
});

describe('feat-029 BR-C-21: the backlog reads again after a save in the card', () => {
  it('shows the row’s new title once the card has saved it, the dialog staying open', async () => {
    const api = serveBoards(dataset());
    renderBoards(backlogAddress());
    const { table, link } = await titleLink();
    await userEvent.click(link);
    await screen.findByRole('dialog', { name: 'Баг MSK-BUG-4' });
    const heading = await screen.findByRole('textbox', { name: 'Заголовок' });
    const reads = api.reads('/backlogs/stories').length;

    fireEvent.change(heading, { target: { value: 'Падение при входе по ссылке' } });
    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() =>
      expect(
        within(table).getByRole('link', { name: 'Падение при входе по ссылке', hidden: true }),
      ).toBeInTheDocument(),
    );
    expect(api.reads('/backlogs/stories').length).toBeGreaterThan(reads);
    expect(screen.getByRole('dialog', { name: 'Баг MSK-BUG-4' })).toBeVisible();
  });
});
