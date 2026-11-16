import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { serveProblem } from '../../../../test/api';
import { renderWithProviders } from '../../../../test/renderWithProviders';
import {
  ORG,
  SPRINTS_ROUTE,
  TEAM,
  serveSprintList,
  sprintId,
  sprintRow,
} from '../../../iterations/testApi';
import type { WorkItemSprint } from '../../api/workItemsApi';
import { CardSprintField } from './CardSprintField';

// mod-007 US-4, design § 7: the card's «Спринт» field.

vi.setConfig({ testTimeout: 120_000 });

const ACTIVE: WorkItemSprint = { id: sprintId(2), name: 'Спринт 2', state: 'Active' };

function renderField(
  props: Partial<Parameters<typeof CardSprintField>[0]> = {},
  onChange = vi.fn(),
) {
  renderWithProviders(
    <CardSprintField
      organizationId={ORG}
      teamId={TEAM}
      value={null}
      cleared={false}
      error={undefined}
      busy={false}
      readOnly={false}
      onChange={onChange}
      {...props}
    />,
  );
  return onChange;
}

const field = () => screen.getByRole('combobox', { name: 'Спринт' });

describe('mod-007 design § 7: «Спринт» of the card', () => {
  it('says «Без спринта» for an item with no sprint', () => {
    renderField();
    expect(field()).toHaveValue('Без спринта');
  });

  it('lists «Без спринта» and the open sprints, an active one marked, and reports the pick', async () => {
    serveSprintList([
      sprintRow(1, { state: 'Closed' }),
      sprintRow(2, { state: 'Active' }),
      sprintRow(3),
    ]);
    const onChange = renderField();

    await userEvent.click(field());
    const options = (await screen.findAllByRole('option')).map((option) => option.textContent);
    expect(options).toEqual(['Без спринта', 'Спринт 3', 'Спринт 2 · активен']);
    await userEvent.click(screen.getByRole('option', { name: 'Спринт 3' }));
    expect(onChange).toHaveBeenCalledWith({ id: sprintId(3), name: 'Спринт 3', state: 'Planned' });
  });

  it('reports «Без спринта» as none', async () => {
    serveSprintList([sprintRow(2, { state: 'Active' })]);
    const onChange = renderField({ value: ACTIVE });

    await userEvent.click(field());
    await userEvent.click(await screen.findByRole('option', { name: 'Без спринта' }));
    expect(onChange).toHaveBeenCalledWith(null);
  });

  it('shows a kept Closed sprint as «завершён»', () => {
    renderField({ value: { id: sprintId(1), name: 'Спринт 1', state: 'Closed' } });
    expect(field()).toHaveValue('Спринт 1 · завершён');
  });

  it('asks for a team first and cannot be opened without one', async () => {
    renderField({ teamId: null });
    expect(field()).toHaveValue('Сначала выберите команду');
    expect(field()).toHaveAttribute('readonly');
  });

  it('says the sprint will be cleared when the team was changed under it', () => {
    renderField({ cleared: true });
    expect(screen.getByText('Спринт будет очищен: он принадлежит прежней команде.')).toBeVisible();
    expect(field()).toHaveAccessibleDescription(
      'Спринт будет очищен: он принадлежит прежней команде.',
    );
  });

  it('says the sprints failed to load and reads again at the next opening', async () => {
    serveProblem('get', SPRINTS_ROUTE, 'organizations.access.forbidden', {}, { status: 500 });
    renderField();

    await userEvent.click(field());
    expect(
      await screen.findByText('Не удалось загрузить спринты. Попробуйте ещё раз.'),
    ).toBeVisible();
    await userEvent.keyboard('{Escape}');

    serveSprintList([sprintRow(3)]);
    await userEvent.click(field());
    expect(await screen.findByRole('option', { name: 'Спринт 3' })).toBeVisible();
  });

  it('shows the refusal under the field', () => {
    renderField({ error: 'Спринт принадлежит другой команде.' });
    expect(screen.getByText('Спринт принадлежит другой команде.')).toBeVisible();
  });
});
