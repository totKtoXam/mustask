import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import { serveWorkItems } from '../testApi';
import { deleteRecord, exampleDataset, recordOf } from '../testFixtures';
import { MSK_LIST, renderWorkItems } from '../testRender';

// mod-005 US-2 and design § 7: «Рабочие элементы» shows no row for a deleted item, the pager
// counts the items that are shown, and the page offers no control and no hint about deleted
// items of its own — feat-003's «Статус» is the way in (its option «Удалена» shows only when the
// field is opened, `WorkItemsPage.filters.test.tsx`), and this line built none (OQ-2 A). Nothing
// is retired: with no state chosen the page still draws nothing that names the deleted.

async function table() {
  return screen.findByRole('table', { name: 'Рабочие элементы' });
}

describe('mod-005 US-2 Scenario: A deleted item leaves the list', () => {
  it('lists the live items only, counts them, and draws nothing that would reach the deleted', async () => {
    const data = exampleDataset();
    deleteRecord(recordOf(data, 4));
    serveWorkItems(data);
    renderWorkItems(MSK_LIST);

    const list = await table();

    expect(within(list).queryByText('MSK-BUG-4')).not.toBeInTheDocument();
    expect(within(list).getByText('MSK-TASK-5')).toBeInTheDocument();
    expect(screen.getByText('1–1 из 1')).toBeInTheDocument();
    expect(screen.queryByText('Показать удалённые', { exact: false })).not.toBeInTheDocument();
    expect(screen.queryByText('Корзина', { exact: false })).not.toBeInTheDocument();
    expect(screen.queryByText('Удалена', { exact: false })).not.toBeInTheDocument();
  });
});
