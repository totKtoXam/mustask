import { describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { recordRequests } from '../../../../test/api';
import { renderWithProviders } from '../../../../test/renderWithProviders';
import { ORG, SPRINTS_ROUTE, TEAM, sprintId, sprintRow } from '../../../iterations/testApi';
import { CardSprintField } from './CardSprintField';

// mod-007 QA (frontend): the card field's read-only state and what it asks the server for.

vi.setConfig({ testTimeout: 120_000 });

function renderField(props: Partial<Parameters<typeof CardSprintField>[0]> = {}) {
  const onChange = vi.fn();
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

describe('mod-007 design § 7 QA: the field of a reader', () => {
  it('shows the value, opens no list when read-only', async () => {
    recordRequests('get', SPRINTS_ROUTE, () => ({ items: [], nextBefore: null }));
    const onChange = renderField({
      readOnly: true,
      value: { id: sprintId(2), name: 'Спринт 2', state: 'Active' },
    });

    expect(field()).toHaveValue('Спринт 2 · активен');
    await userEvent.click(field());
    expect(screen.queryByRole('option')).not.toBeInTheDocument();
    expect(onChange).not.toHaveBeenCalled();
  });
});

describe('mod-007 design § 7 QA: the options', () => {
  it('asks only for the open sprints of the item’s team and offers no Closed one', async () => {
    const reads = recordRequests('get', SPRINTS_ROUTE, () => ({
      items: [sprintRow(3), sprintRow(2, { state: 'Active' })],
      nextBefore: null,
    }));
    renderField();

    await userEvent.click(field());
    expect(await screen.findByRole('option', { name: 'Спринт 2 · активен' })).toBeVisible();
    expect(screen.getByRole('option', { name: 'Без спринта' })).toBeVisible();
    expect(screen.queryByRole('option', { name: /завершён/ })).not.toBeInTheDocument();
    expect(reads.requests[0]?.search).toContain('open=true');
  });

  it('reports a picked sprint with its state', async () => {
    recordRequests('get', SPRINTS_ROUTE, () => ({ items: [sprintRow(3)], nextBefore: null }));
    const onChange = renderField();

    await userEvent.click(field());
    await userEvent.click(await screen.findByRole('option', { name: 'Спринт 3' }));
    expect(onChange).toHaveBeenCalledWith({ id: sprintId(3), name: 'Спринт 3', state: 'Planned' });
  });
});
