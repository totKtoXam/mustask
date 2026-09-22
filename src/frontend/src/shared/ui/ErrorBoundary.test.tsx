import { afterEach, beforeEach, describe, expect, it, onTestFinished, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ErrorBoundary } from './ErrorBoundary';

function Bomb(): never {
  throw new Error('boom');
}

describe('ErrorBoundary', () => {
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    // React логирует ошибку рендера в консоль сам по себе — молчим на время теста.
    consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it('рендерит дочерний контент, если исключений не было', () => {
    render(
      <ErrorBoundary>
        <div>Дочерний контент</div>
      </ErrorBoundary>,
    );

    expect(screen.getByText('Дочерний контент')).toBeInTheDocument();
  });

  it('перехватывает исключение при рендере и показывает резервный экран', () => {
    render(
      <ErrorBoundary>
        <Bomb />
      </ErrorBoundary>,
    );

    expect(screen.getByText('Что-то пошло не так')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Обновить страницу' })).toBeInTheDocument();
    // Подмена содержимого без объявления незаметна для скринридера:
    // резервный экран должен объявляться сам.
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.queryByText('boom')).not.toBeInTheDocument();
    // React 19 сам логирует пойманную ошибку, поэтому «был вызов» — зелёно
    // и без componentDidCatch. Проверяем именно наш вызов: ошибка + стек.
    expect(consoleErrorSpy).toHaveBeenCalledWith(
      expect.objectContaining({ message: 'boom' }),
      expect.anything(),
    );
  });

  it('кнопка "Обновить страницу" — единственный выход из резервного экрана и делает полную перезагрузку', () => {
    // Полная перезагрузка страницы не зависит от состояния React-дерева —
    // она сработает, даже если сломано ровно то, что нужно было бы для
    // программного "восстановления" (роутер, контекст авторизации и т.д.).
    const reloadSpy = vi.fn();
    const originalLocation = Object.getOwnPropertyDescriptor(window, 'location');
    Object.defineProperty(window, 'location', {
      value: { ...window.location, reload: reloadSpy },
      writable: true,
      configurable: true,
    });
    // Подмена location переживала тест и доставалась следующим — вернуть.
    if (originalLocation) {
      onTestFinished(() => {
        Object.defineProperty(window, 'location', originalLocation);
      });
    }

    render(
      <ErrorBoundary>
        <Bomb />
      </ErrorBoundary>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Обновить страницу' }));

    expect(reloadSpy).toHaveBeenCalledTimes(1);
  });

  it('НЕ перехватывает исключение из обработчика события (известное ограничение границы ошибок)', () => {
    // React-границы ошибок ловят только исключения фазы рендера. Исключение
    // из onClick — вне рендера — до границы не доходит: резервный экран не
    // показывается, а сломанный экран просто остаётся как есть, без обратной
    // связи для пользователя. Тест фиксирует эту границу применимости, а не
    // чинит её.
    function EventThrower() {
      return (
        <button
          onClick={() => {
            throw new Error('обработчик события');
          }}
        >
          Нажми меня
        </button>
      );
    }

    const onWindowError = vi.fn((event: ErrorEvent) => {
      event.preventDefault();
    });
    window.addEventListener('error', onWindowError);

    try {
      render(
        <ErrorBoundary>
          <EventThrower />
        </ErrorBoundary>,
      );

      fireEvent.click(screen.getByText('Нажми меня'));

      expect(onWindowError).toHaveBeenCalledTimes(1);
      expect(screen.queryByText('Что-то пошло не так')).not.toBeInTheDocument();
      expect(screen.getByText('Нажми меня')).toBeInTheDocument();
    } finally {
      window.removeEventListener('error', onWindowError);
    }
  });
});
