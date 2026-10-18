import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { serveWorkItems } from '../testApi';
import { emptyDataset, ids, item, listFilterDataset } from '../testFixtures';
import { MSK_LIST, renderWorkItems } from '../testRender';

// feat-003 US-3, BR-WS-9, design § 2.1, D-3 A: «Поиск» over the title and the identifier. The
// field's timing is `KeywordFilter.test.tsx`'s; here the page reads what the keyword finds.

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
const keyword = () => within(band()).getByRole('textbox', { name: 'Поиск' });

async function listed(): Promise<string[]> {
  return rowsOf(await table()).map((row) => cellsOf(row)[0] ?? '');
}

async function expectListed(expected: string[]) {
  await waitFor(async () => expect(await listed()).toEqual(expected));
}

const NOTHING = () => screen.findByRole('heading', { level: 2, name: 'Ничего не найдено' });

describe('feat-003 US-3 Scenario: the keyword finds by title, identifier and number', () => {
  it('a word of a title finds the items that hold it, in the list’s order, and the count follows', async () => {
    const api = serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await userEvent.type(keyword(), 'оплат');

    await expectListed(['MSK-BUG-2', 'MSK-US-1']);
    expect(screen.getByText('1–2 из 2')).toBeInTheDocument();
    expect(api.reads('/workitems?').at(-1)?.path).toContain(`search=${enc('оплат')}`);
    expect(addressSearch()).toBe(`?search=${enc('оплат')}`);
    expect(keyword()).toHaveFocus();
  });

  it('ignores letter case, and reads «ё» as «е» whichever way the title and the keyword write it', async () => {
    const data = emptyDataset();
    data.items.push(
      item(ids.msk, 1, 'TASK', 'Ёлка на площади'),
      item(ids.msk, 2, 'TASK', 'Нарядная елка'),
      item(ids.msk, 3, 'TASK', 'Камин'),
    );
    serveWorkItems(data);
    renderWorkItems(`${MSK_LIST}?search=${enc('ЕЛКА')}`);

    await expectListed(['MSK-TASK-2', 'MSK-TASK-1']);
  });

  it('the identifier finds its item, in any letter case; a part of it, another type’s key and another project’s identifier do not', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?search=${enc('msk-us-3')}`);
    await expectListed(['MSK-US-3']);

    await userEvent.clear(keyword());
    await userEvent.type(keyword(), 'MSK-BUG-4{Enter}');
    await NOTHING();

    for (const text of ['BUG-42', 'MSK-US-42', 'OTH-BUG-42', 'MSK-BUG-042']) {
      await userEvent.clear(keyword());
      await userEvent.type(keyword(), `${text}{Enter}`);
      await waitFor(async () => expect(addressSearch()).toBe(`?search=${enc(text)}`));
      await NOTHING();
    }
  });

  it('the number finds its item and the titles that hold it', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?search=42`);

    await expectListed(['MSK-BUG-42', 'MSK-TASK-4']);
  });

  it('«%» finds only itself', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?search=${enc('%')}`);

    await expectListed(['MSK-US-3']);
  });

  it('a keyword is trimmed: the address carries the trimmed text, and leaving the field shows it trimmed', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await userEvent.type(keyword(), '  оплат  ');
    await userEvent.tab();

    await expectListed(['MSK-BUG-2', 'MSK-US-1']);
    expect(addressSearch()).toBe(`?search=${enc('оплат')}`);
    expect(keyword()).toHaveValue('оплат');
  });

  it('a blank keyword sets no filter: spaces leave the address and the list as they were', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await userEvent.type(keyword(), '   ');
    await userEvent.tab();

    expect(addressSearch()).toBe('');
    expect(keyword()).toHaveValue('');
    expect(within(band()).queryByRole('button', { name: 'Очистить фильтры' })).toBeNull();
  });

  // design § 5 "Filtered": a value that names nothing is a filter that is on, a blank value is left
  // out and sets none (BR-WS-9, BR-WS-11; review-code CR-1).
  it('a link whose only keyword is blank is no filter: the whole list, nothing to clear', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?search=%20%20`);
    await table();

    expect(await listed()).toHaveLength(5);
    expect(keyword()).toHaveValue('');
    expect(within(band()).queryByRole('button', { name: 'Очистить фильтры' })).toBeNull();
  });

  it('Enter applies the text at once', async () => {
    const api = serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST);
    await table();

    await userEvent.type(keyword(), 'оплат{Enter}');

    await waitFor(() => expect(addressSearch()).toBe(`?search=${enc('оплат')}`));
    await expectListed(['MSK-BUG-2', 'MSK-US-1']);
    expect(api.reads('/workitems?').filter((read) => read.path.includes('search='))).toHaveLength(
      1,
    );
  });
});

describe('feat-003 BR-WS-9, BR-WS-11 the keyword in the address', () => {
  it('a link that carries it fills the field with the text and lists what it finds', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?search=${enc('оплат')}`);

    await expectListed(['MSK-BUG-2', 'MSK-US-1']);
    expect(keyword()).toHaveValue('оплат');
    expect(within(band()).getByRole('button', { name: 'Очистить фильтры' })).toBeInTheDocument();
  });

  it('a keyword longer than 255 characters in a link is shown whole and lists nothing', async () => {
    serveWorkItems(listFilterDataset());
    const longer = 'я'.repeat(300);
    renderWorkItems(`${MSK_LIST}?search=${longer}`);

    await NOTHING();
    expect(keyword()).toHaveValue(longer);
    expect(keyword()).toHaveAttribute('maxlength', '255');
  });

  it('given more than once, the first non-blank value counts', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?search=%20&search=${enc('оплат')}&search=${enc('картой')}`);

    await expectListed(['MSK-BUG-2', 'MSK-US-1']);
    expect(keyword()).toHaveValue('оплат');
  });

  it('combines with the other filters: an item is listed when it matches every filter that is set', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(
      `${MSK_LIST}?search=${enc('оплат')}&state=${enc('Активна')}&assigneeId=${ids.mia}&teamId=${ids.marsTeam}`,
    );

    await expectListed(['MSK-US-1']);
  });

  it('a deleted item is found by its title or identifier only while «Удалена» is chosen', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(`${MSK_LIST}?search=MSK-FEAT-6`);
    await NOTHING();

    await userEvent.click(within(band()).getByRole('combobox', { name: 'Статус' }));
    await userEvent.click(await screen.findByRole('option', { name: 'Удалена' }));
    await userEvent.keyboard('{Escape}');

    await expectListed(['MSK-FEAT-6']);
  });

  it('a reader searches too', async () => {
    serveWorkItems(listFilterDataset());
    renderWorkItems(MSK_LIST, { role: 'viewer' });
    await table();

    await userEvent.type(keyword(), 'оплат{Enter}');

    await expectListed(['MSK-BUG-2', 'MSK-US-1']);
  });
});
