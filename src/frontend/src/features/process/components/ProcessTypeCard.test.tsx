import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import type { WorkItemType } from '../api/processApi';
import { cloneProcessDefinition, sampleProcessDefinition } from '../testFixtures';
import { ProcessTypeCard } from './ProcessTypeCard';

function typeNamed(name: string): WorkItemType {
  const type = sampleProcessDefinition.workItemTypes.find((candidate) => candidate.name === name);
  if (!type) throw new Error(`No type «${name}» in the fixture.`);
  return type;
}

describe('ProcessTypeCard — one type, read-only (design DD-1, layout B)', () => {
  it('is a section named by its level-2 heading, the type mark hidden before the name', () => {
    render(<ProcessTypeCard type={typeNamed('Эпик')} />);

    const section = screen.getByRole('region', { name: 'Эпик' });
    const heading = within(section).getByRole('heading', { level: 2, name: 'Эпик' });
    const icon = within(heading).getByTestId('CrownIcon');
    expect(icon).toHaveAttribute('aria-hidden', 'true');
    expect(section).not.toHaveTextContent('epic');
  });

  it('lists «Ключ» and «Уровень бэклога» as a description list, without «Оформление»', () => {
    const { container } = render(<ProcessTypeCard type={typeNamed('Баг')} />);

    const list = container.querySelector('dl') as HTMLElement;
    expect(Array.from(list.querySelectorAll('dt')).map((term) => term.textContent)).toEqual([
      'Ключ',
      'Уровень бэклога',
    ]);
    expect(Array.from(list.querySelectorAll('dd')).map((value) => value.textContent)).toEqual([
      'BUG',
      'Истории',
    ]);
    expect(screen.queryByText('Оформление')).not.toBeInTheDocument();
  });

  it('names the states table, heads its columns with scope=col and its rows by the status', () => {
    render(<ProcessTypeCard type={typeNamed('Задача')} />);

    const table = screen.getByRole('table', { name: 'Статусы типа «Задача»' });
    const columns = within(table).getAllByRole('columnheader');
    expect(columns.map((column) => column.textContent)).toEqual([
      'Порядок',
      'Статус',
      'Категория статуса',
    ]);
    columns.forEach((column) => expect(column).toHaveAttribute('scope', 'col'));
    const rowHeaders = within(table).getAllByRole('rowheader');
    expect(rowHeaders.map((header) => header.textContent)).toEqual([
      'НоваяНачальный',
      'Активна',
      'Закрыта',
      'Удалена',
    ]);
    rowHeaders.forEach((header) => {
      expect(header.tagName).toBe('TH');
      expect(header).toHaveAttribute('scope', 'row');
    });
  });

  it('marks only the initial state with an inert «Начальный» chip', () => {
    render(<ProcessTypeCard type={typeNamed('Фича')} />);

    const chips = screen.getAllByText('Начальный');
    expect(chips).toHaveLength(1);
    const chip = chips[0]!.closest('.MuiChip-root') as HTMLElement;
    expect(chip.closest('th')).toHaveTextContent('Новая');
    expect(chip).not.toHaveAttribute('tabindex');
    expect(chip).not.toHaveAttribute('role', 'button');
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('draws an unknown appearance token with the theme’s fallback icon, and nothing fails', () => {
    const type = cloneProcessDefinition().workItemTypes[0]!;
    type.appearance = 'rocket' as WorkItemType['appearance'];

    render(<ProcessTypeCard type={type} />);

    const heading = screen.getByRole('heading', { level: 2, name: 'Эпик' });
    expect(within(heading).getByTestId('HelpOutlineOutlinedIcon')).toBeInTheDocument();
    expect(screen.queryByText('rocket')).not.toBeInTheDocument();
  });
});
