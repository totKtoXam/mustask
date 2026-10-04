import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FormSkeleton } from './FormSkeleton';

describe('FormSkeleton', () => {
  it('несёт role=status и aria-label "Загрузка данных"', () => {
    render(<FormSkeleton />);
    expect(screen.getByRole('status', { name: 'Загрузка данных' })).toBeInTheDocument();
  });

  it('число полей параметризуемо', () => {
    const { container } = render(<FormSkeleton fields={4} />);
    // Каждое поле — подпись + прямоугольник, т.е. 2 Skeleton-узла.
    expect(container.querySelectorAll('.MuiSkeleton-root')).toHaveLength(4 * 2);
  });

  it('по умолчанию рендерит 3 поля', () => {
    const { container } = render(<FormSkeleton />);
    expect(container.querySelectorAll('.MuiSkeleton-root')).toHaveLength(3 * 2);
  });
});
