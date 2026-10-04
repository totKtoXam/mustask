import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RegistrySkeleton } from './RegistrySkeleton';

describe('RegistrySkeleton', () => {
  it('рендерит настоящие заголовки колонок и 5 строк-заглушек по умолчанию', () => {
    const columns = ['Код', 'Эндоним', 'Порядок'];
    render(<RegistrySkeleton columns={columns} />);

    columns.forEach((column) => expect(screen.getByText(column)).toBeInTheDocument());
    expect(screen.getAllByRole('row')).toHaveLength(1 + 5); // заголовок + 5 строк
  });

  it('число строк параметризуемо', () => {
    render(<RegistrySkeleton columns={['Код']} rows={2} />);
    expect(screen.getAllByRole('row')).toHaveLength(1 + 2);
  });

  it('несёт role=status и aria-label "Загрузка данных"', () => {
    render(<RegistrySkeleton columns={['Код']} />);
    expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
  });
});
