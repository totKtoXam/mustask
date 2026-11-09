import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { GrantRow } from '../api/grantsApi';
import { ids } from '../testFixtures';
import { people } from '../testMembersData';
import { openPage, rowsOf } from '../testMembersPage';

// mod-019 plan-frontend X-5: a project with 200 grants. The list is one table over a client-side filter and
// has no paging (design § 3), so every row is drawn, the filter narrows them, and a row menu still works.

const PROJECT_PATH = `/organizations/${ids.alfa}/projects/${ids.prm}/access`;
const COUNT = 200;

function manyGrants(): GrantRow[] {
  return Array.from({ length: COUNT }, (_, index) => {
    const number = String(index + 1).padStart(3, '0');
    return {
      user: {
        id: `b0000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`,
        displayName: `Сотрудник ${number}`,
        email: `worker${number}@example.test`,
        state: 'active',
      },
      role: index % 3 === 0 ? 'manager' : index % 3 === 1 ? 'member' : 'reader',
      origin: 'held',
      department: null,
      givenBy: { ...people.ivan },
      givenAt: '2026-10-20T10:00:00Z',
    } as GrantRow;
  });
}

describe('mod-019 grants: a project with 200 grants', () => {
  it('draws every row, narrows them by the filter and keeps the row menu working', async () => {
    const { organizations } = openPage({ path: PROJECT_PATH });
    organizations.grants[`project:${ids.prm}`] = manyGrants();

    const table = await screen.findByRole('table', { name: 'Доступ', hidden: true });
    await waitFor(() => expect(rowsOf(table)).toHaveLength(COUNT));
    expect(rowsOf(table)[0]?.[0]).toBe('Сотрудник 001');
    expect(rowsOf(table)[COUNT - 1]?.[0]).toBe('Сотрудник 200');

    await userEvent.type(screen.getByRole('textbox', { name: 'Найти участника' }), 'Сотрудник 17');
    await waitFor(() => expect(rowsOf(table)).toHaveLength(10));
    expect(rowsOf(table).every((cells) => cells[0]?.startsWith('Сотрудник 17'))).toBe(true);

    await userEvent.click(screen.getByRole('button', { name: 'Ещё действия: Сотрудник 170' }));
    const menu = await screen.findByRole('menu');
    expect(within(menu).getByRole('menuitem', { name: 'Изменить роль' })).toBeVisible();
    expect(within(menu).getByRole('menuitem', { name: 'Отозвать доступ' })).toBeVisible();
  }, 60_000);
});
