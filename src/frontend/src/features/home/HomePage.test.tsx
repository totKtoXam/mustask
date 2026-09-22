import { describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import { renderWithProviders } from '../../test/renderWithProviders';
import { HomePage } from './HomePage';

describe('HomePage', () => {
  it('показывает заголовок и приветствие без обращений к API', () => {
    // Сетевых запросов у страницы нет: MSW настроен с onUnhandledRequest:
    // 'error', так что лишний запрос уронил бы тест.
    renderWithProviders(<HomePage />);

    expect(screen.getByRole('heading', { name: 'Главная' })).toBeInTheDocument();
    expect(screen.getByText('Добро пожаловать в Mustask')).toBeInTheDocument();
  });
});
