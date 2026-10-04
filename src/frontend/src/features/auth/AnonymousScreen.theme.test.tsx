import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { theme } from '../../app/theme';
import { AuthContext, type AuthContextValue } from './AuthContext';
import { AnonymousScreen } from './AnonymousScreen';

function renderState(overrides: Partial<AuthContextValue>) {
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
  return render(
    <AuthContext.Provider value={value}>
      <AnonymousScreen />
    </AuthContext.Provider>,
  );
}

/** `#F8F8F8` → `rgb(248, 248, 248)`, the form jsdom's computed style reports. */
function rgb(hex: string): string {
  const digits = hex.replace('#', '');
  const [red, green, blue] = [0, 2, 4].map((offset) =>
    parseInt(digits.slice(offset, offset + 2), 16),
  );
  return `rgb(${red}, ${green}, ${blue})`;
}

describe('US-11 Scenario: The panel’s auth screens on the theme', () => {
  it.each<[string, Partial<AuthContextValue>, string, string | null]>([
    ['restoring', { status: 'initializing' }, 'Выполняется вход…', null],
    ['signed out', { reason: 'signed_out' }, 'Вы вышли из системы.', 'Войти'],
    ['login failed', { reason: 'login_failed' }, 'Вход не выполнен.', 'Войти'],
    ['expired', { reason: 'expired' }, 'Сессия истекла, войдите заново.', 'Войти'],
    [
      'auth unavailable',
      { reason: 'auth_unavailable' },
      'Сервис аутентификации недоступен, повторите позже.',
      'Повторить',
    ],
    [
      'sign-out failed',
      { reason: 'logout_failed' },
      'Выход не завершён: сервис аутентификации недоступен.',
      'Повторить выход',
    ],
  ])(
    '%s: a centred outlined card on the application background, headed «Mustask», outside the shell, tab «Mustask»',
    (_state, overrides, text, action) => {
      document.title = 'Языки — Mustask';
      const { container } = renderState(overrides);

      expect(screen.getByRole('heading', { level: 1, name: 'Mustask' })).toBeInTheDocument();
      expect(screen.getByText(text)).toBeInTheDocument();
      expect(document.title).toBe('Mustask');
      expect(screen.queryByRole('banner')).not.toBeInTheDocument();
      expect(screen.queryByRole('navigation')).not.toBeInTheDocument();

      const card = container.querySelector('.MuiCard-root') as HTMLElement;
      expect(card).toHaveClass('MuiPaper-outlined');
      expect(getComputedStyle(card).width).toBe(`${theme.layout.authCardWidth}px`);
      expect(getComputedStyle(card.parentElement as HTMLElement).backgroundColor).toBe(
        rgb(theme.palette.background.default),
      );

      if (action) {
        const button = screen.getByRole('button', { name: action });
        expect(button).toHaveClass('MuiButton-contained');
        expect(button).toHaveClass('MuiButton-fullWidth');
      } else {
        expect(screen.queryByRole('button')).not.toBeInTheDocument();
      }
    },
  );
});
