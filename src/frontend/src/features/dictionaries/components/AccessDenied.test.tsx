import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AccessDenied } from './AccessDenied';

describe('AccessDenied', () => {
  it('показывает заголовок и текст канона дословно', () => {
    render(<AccessDenied />);

    expect(screen.getByText('Доступ ограничен')).toBeInTheDocument();
    expect(
      screen.getByText(
        'У вас нет прав для просмотра этого раздела. Обратитесь к администратору, чтобы запросить доступ.',
      ),
    ).toBeInTheDocument();
  });
});
