import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

// Падение отрисовывают маршруты — то есть исключение возникает под всеми
// провайдерами. Если граница ошибок уйдёт из App.tsx, резервного экрана не
// будет и тест это скажет: сам компонент можно проверить отдельно, а вот
// что приложение им обёрнуто — только здесь.
vi.mock('./routes', () => ({
  AppRoutes: () => {
    throw new Error('boom');
  },
}));

describe('App', () => {
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it('перехватывает падение маршрута и показывает резервный экран вместо пустой страницы', async () => {
    const { App } = await import('./App');

    render(<App />);

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Что-то пошло не так')).toBeInTheDocument();
    expect(screen.queryByText('boom')).not.toBeInTheDocument();
  });
});
