import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { department, exampleDataset, ids } from '../testFixtures';
import { serveOrganizations } from '../testApi';
import { renderPage } from '../testRender';

const PLATFORM_TEAM = `/organizations/${ids.alfa}/settings/teams/${ids.platformTeam}`;

/** mod-018 design D-1: the picker draws at most 50 matches and says how many there are. */
describe('mod-018 D-1: the department picker caps its list', () => {
  function manyDepartments() {
    const data = exampleDataset();
    for (let index = 0; index < 60; index += 1) {
      data.areas.push(
        department(
          `e0000000-0000-4000-8000-${String(index).padStart(12, '0')}`,
          ids.alfa,
          `Лишний ${String(index).padStart(2, '0')}`,
        ),
      );
    }
    return data;
  }

  it('draws 50 options and «Показаны первые 50 из N. Уточните запрос.», narrowing by typing', async () => {
    serveOrganizations(manyDepartments());
    renderPage({ path: PLATFORM_TEAM });
    await screen.findByRole('table', { name: 'Департаменты команды' });
    await userEvent.click(screen.getByRole('button', { name: 'Добавить департамент' }));
    const dialog = await screen.findByRole('dialog', {
      name: 'Добавление департаментов в команду',
    });
    await userEvent.click(within(dialog).getByRole('combobox', { name: /Департаменты/ }));

    const list = await screen.findByRole('listbox');
    const options = within(list).getAllByRole('option');
    // 50 departments and the disabled hint line.
    expect(options).toHaveLength(51);
    expect(within(list).getByText(/^Показаны первые 50 из \d+\. Уточните запрос\.$/)).toBeVisible();

    await userEvent.type(
      within(dialog).getByRole('combobox', { name: /Департаменты/ }),
      'Лишний 07',
    );
    await waitFor(() =>
      expect(
        within(screen.getByRole('listbox'))
          .getAllByRole('option')
          .map((option) => option.textContent),
      ).toEqual(['Лишний 07']),
    );
  });
});
