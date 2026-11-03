import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SearchMenu, type SearchMenuGroup, type SearchMenuProps } from './SearchMenu';

// feat-043 design § 3: a header selector — a caption over a value, a search, a «все» row,
// a fitting group and «Остальные», the states, the keys.

const GROUPS: readonly SearchMenuGroup[] = [
  {
    options: [
      { id: 'a', label: 'A', hint: 'KEY-A' },
      { id: 'b', label: 'Склад' },
    ],
  },
  {
    heading: 'Остальные',
    note: 'Выбор сбросит то, что не подойдёт.',
    options: [{ id: 'c', label: 'Финансы' }],
  },
];

function Demo(props: Partial<SearchMenuProps>) {
  return (
    <SearchMenu
      label="Проект"
      value="все"
      allLabel="все"
      groups={GROUPS}
      searchLabel="Найти…"
      emptyText="Ничего не найдено"
      onSelect={vi.fn()}
      {...props}
    />
  );
}

const trigger = () => screen.getByRole('combobox', { name: 'Проект: все', hidden: true });

describe('feat-043 Scenario: SearchMenu', () => {
  it('names the trigger by caption and value, and opens a labelled listbox', async () => {
    const user = userEvent.setup();
    render(<Demo />);
    expect(trigger()).toHaveAttribute('aria-expanded', 'false');
    expect(trigger()).toHaveAttribute('aria-haspopup', 'listbox');
    await user.click(trigger());
    expect(trigger()).toHaveAttribute('aria-expanded', 'true');
    const list = screen.getByRole('listbox', { name: 'Проект' });
    expect(
      within(list)
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['все', 'AKEY-A', 'Склад', 'Финансы']);
  });

  it('shows «все» first, the fitting group, then the headed group with its note', async () => {
    const user = userEvent.setup();
    render(<Demo />);
    await user.click(trigger());
    const group = screen.getByRole('group', { name: 'Остальные' });
    expect(within(group).getByText('Выбор сбросит то, что не подойдёт.')).toBeVisible();
    expect(within(group).getAllByRole('option')).toHaveLength(1);
  });

  it('marks the chosen option', async () => {
    const user = userEvent.setup();
    render(<Demo value="A" chosen currentId="a" />);
    await user.click(screen.getByRole('combobox', { name: 'Проект: A', hidden: true }));
    expect(screen.getByRole('option', { name: /^A/ })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('option', { name: 'все' })).toHaveAttribute('aria-selected', 'false');
  });

  it('chooses an option, closes, and reports its id', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Demo onSelect={onSelect} />);
    await user.click(trigger());
    await user.click(screen.getByRole('option', { name: 'Финансы' }));
    expect(onSelect).toHaveBeenCalledWith('c');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('reports null for the «все» row', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Demo onSelect={onSelect} value="A" currentId="a" />);
    await user.click(screen.getByRole('combobox', { name: 'Проект: A', hidden: true }));
    await user.click(screen.getByRole('option', { name: 'все' }));
    expect(onSelect).toHaveBeenCalledWith(null);
  });

  it('narrows both groups by a part of a name or of the hint, ignoring case', async () => {
    const user = userEvent.setup();
    render(<Demo />);
    await user.click(trigger());
    await user.type(screen.getByRole('textbox', { name: 'Найти…' }), 'фин');
    expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual(['Финансы']);
    await user.clear(screen.getByRole('textbox', { name: 'Найти…' }));
    await user.type(screen.getByRole('textbox', { name: 'Найти…' }), 'key-a');
    expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual(['AKEY-A']);
  });

  it('says «Ничего не найдено» when nothing matches', async () => {
    const user = userEvent.setup();
    render(<Demo />);
    await user.click(trigger());
    await user.type(screen.getByRole('textbox', { name: 'Найти…' }), 'zzz');
    expect(screen.getByText('Ничего не найдено')).toBeVisible();
    expect(screen.queryAllByRole('option')).toHaveLength(0);
  });

  it('keeps the footer outside the search', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Demo onSelect={onSelect} footer={{ id: 'all-orgs', label: 'Все организации' }} />);
    await user.click(trigger());
    await user.type(screen.getByRole('textbox', { name: 'Найти…' }), 'фин');
    await user.click(screen.getByRole('option', { name: 'Все организации' }));
    expect(onSelect).toHaveBeenCalledWith('all-orgs');
  });

  it('moves with the arrows, chooses with Enter', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Demo onSelect={onSelect} />);
    await user.click(trigger());
    await user.keyboard('{ArrowDown}{ArrowDown}');
    expect(screen.getByRole('option', { name: /^A/ })).toHaveFocus();
    await user.keyboard('{End}');
    expect(screen.getByRole('option', { name: 'Финансы' })).toHaveFocus();
    await user.keyboard('{Home}{ArrowUp}');
    expect(screen.getByRole('textbox', { name: 'Найти…' })).toHaveFocus();
    await user.keyboard('{ArrowDown}{ArrowDown}{Enter}');
    expect(onSelect).toHaveBeenCalledWith('a');
  });

  it('opens on ArrowDown at the trigger and gives focus back on Escape', async () => {
    const user = userEvent.setup();
    render(<Demo />);
    trigger().focus();
    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('listbox')).toBeVisible();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(trigger()).toHaveFocus();
  });

  it('shows one status skeleton while loading, no options', async () => {
    const user = userEvent.setup();
    render(<Demo status="loading" />);
    await user.click(trigger());
    expect(screen.getByRole('status', { name: /Загрузка данных/ })).toBeInTheDocument();
    expect(screen.queryByRole('option')).not.toBeInTheDocument();
  });

  it('shows the error with a retry that calls the host', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(
      <Demo
        status="error"
        errorText="Не удалось загрузить список. Попробуйте ещё раз."
        retryLabel="Повторить"
        onRetry={onRetry}
      />,
    );
    await user.click(trigger());
    expect(screen.getByRole('alert')).toHaveTextContent('Не удалось загрузить список.');
    await user.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetry).toHaveBeenCalledOnce();
  });
});
