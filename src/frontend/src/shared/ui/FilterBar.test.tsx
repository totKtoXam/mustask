import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import TextField from '@mui/material/TextField';
import { FilterBar } from './FilterBar';

describe('US-8 Scenario: Filter bar above the table', () => {
  it('holds the fields in one group named «Фильтры» and shows no clear control while no filter is set', () => {
    render(
      <FilterBar active={false} onClear={() => {}}>
        <TextField label="Код" />
        <TextField label="Эндоним" />
      </FilterBar>,
    );

    const group = screen.getByRole('group', { name: 'Фильтры' });
    expect(within(group).getByLabelText('Код')).toBeInTheDocument();
    expect(within(group).getByLabelText('Эндоним')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Очистить фильтры' })).not.toBeInTheDocument();
  });
});

describe('US-8 Scenario: Clearing all filters from the bar', () => {
  it('offers «Очистить фильтры» as a text button while a filter is set, and it clears', async () => {
    const onClear = vi.fn();
    render(
      <FilterBar active onClear={onClear}>
        <TextField label="Код" defaultValue="kk" />
      </FilterBar>,
    );

    const clear = screen.getByRole('button', { name: 'Очистить фильтры' });
    expect(clear).toHaveClass('MuiButton-text');
    await userEvent.click(clear);

    expect(onClear).toHaveBeenCalledTimes(1);
  });
});
