import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AuthContext, type AuthContextValue } from '../../auth/AuthContext';
import { RequirePermission } from './RequirePermission';

function renderWithCan(can: (code: string) => boolean, props: { any: string[] } | { all: string[] }) {
  const value: AuthContextValue = {
    status: 'authenticated',
    reason: null,
    loginFailedCode: null,
    displayName: 'Иванов Иван',
    can,
    signIn: vi.fn(),
    signOut: vi.fn(),
    retryBootstrap: vi.fn(),
    retrySignOut: vi.fn(),
  };
  render(
    <AuthContext.Provider value={value}>
      <RequirePermission {...props} fallback={<div>DENIED</div>}>
        <div>ALLOWED</div>
      </RequirePermission>
    </AuthContext.Provider>,
  );
}

describe('RequirePermission', () => {
  describe('режим any', () => {
    it('один код, есть — рендерит children', () => {
      renderWithCan((code) => code === 'a', { any: ['a'] });
      expect(screen.getByText('ALLOWED')).toBeInTheDocument();
    });

    it('один код, нет — рендерит fallback', () => {
      renderWithCan(() => false, { any: ['a'] });
      expect(screen.getByText('DENIED')).toBeInTheDocument();
    });

    it('два кода, есть хотя бы один — рендерит children', () => {
      renderWithCan((code) => code === 'b', { any: ['a', 'b'] });
      expect(screen.getByText('ALLOWED')).toBeInTheDocument();
    });

    it('два кода, нет ни одного — рендерит fallback', () => {
      renderWithCan(() => false, { any: ['a', 'b'] });
      expect(screen.getByText('DENIED')).toBeInTheDocument();
    });
  });

  describe('режим all', () => {
    it('два кода, оба есть — рендерит children', () => {
      renderWithCan(() => true, { all: ['a', 'b'] });
      expect(screen.getByText('ALLOWED')).toBeInTheDocument();
    });

    it('два кода, только один есть (смешанный случай) — рендерит fallback', () => {
      renderWithCan((code) => code === 'a', { all: ['a', 'b'] });
      expect(screen.getByText('DENIED')).toBeInTheDocument();
    });

    it('два кода, ни одного нет — рендерит fallback', () => {
      renderWithCan(() => false, { all: ['a', 'b'] });
      expect(screen.getByText('DENIED')).toBeInTheDocument();
    });
  });

  // раньше пустой/забытый список условий читался как «нечего
  // проверять — пускаем» (fail open). Теперь это невозможно: на уровне типов —
  // `any`/`all` взаимоисключающие и обязательные (проверяется ниже через
  // `@ts-expect-error`), а на уровне рантайма — пустой массив тоже отказывает,
  // как защита в глубину поверх типовой гарантии.
  describe('отказ по умолчанию', () => {
    it('any — пустой список кодов отказывает, а не пускает', () => {
      renderWithCan(() => true, { any: [] });
      expect(screen.getByText('DENIED')).toBeInTheDocument();
    });

    it('all — пустой список кодов отказывает, а не пускает', () => {
      renderWithCan(() => true, { all: [] });
      expect(screen.getByText('DENIED')).toBeInTheDocument();
    });

    it('ни `any`, ни `all` вовсе — типы это запрещают; в обход типов рантайм всё равно отказывает', () => {
      // @ts-expect-error — намеренно обходим типовую защиту (ни `any`, ни
      // `all` не передан), чтобы доказать, что рантайм тоже не пускает молча.
      renderWithCan(() => true, {});
      expect(screen.getByText('DENIED')).toBeInTheDocument();
    });
  });
});
