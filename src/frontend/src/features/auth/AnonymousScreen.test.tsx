import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AuthContext, type AuthContextValue } from './AuthContext';
import { AnonymousScreen } from './AnonymousScreen';

function renderWithAuth(overrides: Partial<AuthContextValue>) {
  const value: AuthContextValue = {
    status: 'anonymous',
    reason: null,
    loginFailedCode: null,
    displayName: '',
    can: () => false,
    signIn: vi.fn(),
    signOut: vi.fn(),
    retryBootstrap: vi.fn(),
    retrySignOut: vi.fn(),
    ...overrides,
  };
  render(
    <AuthContext.Provider value={value}>
      <AnonymousScreen />
    </AuthContext.Provider>,
  );
  return value;
}

describe('AnonymousScreen', () => {
  it('initializing — спиннер и текст "Выполняется вход…", без кнопки', () => {
    renderWithAuth({ status: 'initializing', reason: null });

    expect(screen.getByLabelText('Загрузка')).toBeInTheDocument();
    expect(screen.getByText('Выполняется вход…')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('signed_out — "Вы вышли из системы.", "Войти" зовёт signIn("/home")', () => {
    const value = renderWithAuth({ reason: 'signed_out' });

    expect(screen.getByText('Вы вышли из системы.')).toBeInTheDocument();
    screen.getByRole('button', { name: 'Войти' }).click();
    expect(value.signIn).toHaveBeenCalledWith('/home');
  });

  it.each([
    ['access_denied', 'Вход отменён на странице входа.'],
    ['idp_unavailable', 'Сервис аутентификации недоступен, повторите позже.'],
    ['sign_in_failed', 'Не удалось завершить вход, попробуйте ещё раз.'],
    [null, 'Предыдущая попытка входа не была завершена.'],
    ['unknown_code', 'Предыдущая попытка входа не была завершена.'],
  ])('login_failed(%s) — "Вход не выполнен." + текст по коду', (code, text) => {
    const value = renderWithAuth({ reason: 'login_failed', loginFailedCode: code });

    expect(screen.getByText('Вход не выполнен.')).toBeInTheDocument();
    expect(screen.getByText(text)).toBeInTheDocument();
    screen.getByRole('button', { name: 'Войти' }).click();
    expect(value.signIn).toHaveBeenCalledWith();
  });

  it('expired — "Сессия истекла, войдите заново.", "Войти" зовёт signIn() без аргумента', () => {
    const value = renderWithAuth({ reason: 'expired' });

    expect(screen.getByText('Сессия истекла, войдите заново.')).toBeInTheDocument();
    screen.getByRole('button', { name: 'Войти' }).click();
    expect(value.signIn).toHaveBeenCalledWith();
  });

  it('auth_unavailable — текст сервиса, "Повторить" зовёт retryBootstrap', () => {
    const value = renderWithAuth({ reason: 'auth_unavailable' });

    expect(screen.getByText('Сервис аутентификации недоступен, повторите позже.')).toBeInTheDocument();
    screen.getByRole('button', { name: 'Повторить' }).click();
    expect(value.retryBootstrap).toHaveBeenCalledTimes(1);
  });

  it('logout_failed — текст выхода, "Повторить выход" зовёт retrySignOut', () => {
    const value = renderWithAuth({ reason: 'logout_failed' });

    expect(screen.getByText('Выход не завершён: сервис аутентификации недоступен.')).toBeInTheDocument();
    screen.getByRole('button', { name: 'Повторить выход' }).click();
    expect(value.retrySignOut).toHaveBeenCalledTimes(1);
  });
});
