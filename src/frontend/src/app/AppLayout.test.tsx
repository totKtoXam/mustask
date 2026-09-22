import { describe, expect, it, vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { render, screen } from '@testing-library/react';
import { AuthContext, type AuthContextValue } from '../features/auth/AuthContext';
import { dictionariesPermissions } from '../features/dictionaries/permissions';
import { AppLayout } from './AppLayout';

function renderLayout(overrides: Partial<AuthContextValue>) {
  const value: AuthContextValue = {
    status: 'authenticated',
    reason: null,
    loginFailedCode: null,
    displayName: 'Иванов Иван',
    can: () => true,
    signIn: vi.fn(),
    signOut: vi.fn(),
    retryBootstrap: vi.fn(),
    retrySignOut: vi.fn(),
    ...overrides,
  };
  render(
    <AuthContext.Provider value={value}>
      <MemoryRouter initialEntries={['/home']}>
        <Routes>
          <Route element={<AppLayout />}>
            <Route path="/home" element={<div>HOME_PAGE</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  );
  return value;
}

describe('AppLayout', () => {
  it('показывает имя из токена в шапке', () => {
    renderLayout({ displayName: 'Иванов Иван' });

    expect(screen.getByText('Иванов Иван')).toBeInTheDocument();
  });

  it('"Выйти" вызывает signOut', () => {
    const value = renderLayout({});

    screen.getByRole('button', { name: /Выйти/ }).click();

    expect(value.signOut).toHaveBeenCalledTimes(1);
  });

  it('показывает название приложения в шапке', () => {
    renderLayout({});

    expect(screen.getByRole('heading', { name: 'Mustask' })).toBeInTheDocument();
  });

  it('с languagesRead — пункт "Языки" показан', () => {
    renderLayout({ can: () => true });

    expect(screen.getByText('Языки')).toBeInTheDocument();
  });

  it('без languagesRead — пункт "Языки" скрыт, негейтированные пункты остаются', () => {
    renderLayout({ can: (code) => code !== dictionariesPermissions.languagesRead });

    expect(screen.queryByText('Языки')).not.toBeInTheDocument();
    expect(screen.getByText('Главная')).toBeInTheDocument();
  });
});
