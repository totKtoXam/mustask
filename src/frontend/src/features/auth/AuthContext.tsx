/* eslint-disable react-refresh/only-export-components -- контекст и hook
   экспортируются вместе с провайдером сознательно: это один срез auth. */
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { decodeAccessToken } from '../../shared/api/tokenClaims';
import {
  SessionLostError,
  getAccessToken,
  setSessionErrorHandler,
  setSessionTokenHandler,
  signIn as sessionSignIn,
  signOut as sessionSignOut,
} from '../../shared/api/session';

/** Причины экрана `anonymous` — тексты и кнопки см. `AnonymousScreen`. */
export type AnonymousReason =
  | 'signed_out'
  | 'login_failed'
  | 'expired'
  | 'auth_unavailable'
  | 'logout_failed';

type AuthState =
  | { status: 'initializing' }
  | { status: 'authenticated'; displayName: string; permissions: string[] }
  | { status: 'anonymous'; reason: AnonymousReason; loginFailedCode: string | null };

export interface AuthContextValue {
  status: AuthState['status'];
  /** Осмыслен только при `status === 'anonymous'`. */
  reason: AnonymousReason | null;
  /** Код `authError` из URL — осмыслен только при `reason === 'login_failed'`. */
  loginFailedCode: string | null;
  displayName: string;
  /** Есть ли у пользователя право (коды из PermissionCatalog). */
  can: (permission: string) => boolean;
  signIn: (returnTo?: string) => void;
  signOut: () => void;
  retryBootstrap: () => void;
  retrySignOut: () => void;
}

// Экспортирован сверх `AuthProvider`/`useAuth` только ради тестов
// `AnonymousScreen`/`AppLayout` — им нужно подставить конкретное состояние
// без полного прогона bootstrap-эффекта через сеть на каждый сценарий.
export const AuthContext = createContext<AuthContextValue | null>(null);

const AUTH_ERROR_PARAM = 'authError';

function currentPath(): string {
  return `${window.location.pathname}${window.location.search}`;
}

function stripAuthErrorFromUrl(): void {
  const url = new URL(window.location.href);
  url.searchParams.delete(AUTH_ERROR_PARAM);
  window.history.replaceState(null, '', `${url.pathname}${url.search}${url.hash}`);
}

/**
 * Что решить по URL **на момент монтирования**, без сети: три случая.
 * Вынесено из эффекта в чистую функцию, вызванную из ленивого инициализатора
 * `useState`, — синхронные переходы в `anonymous` тем самым не требуют
 * `setState` внутри эффекта; сетевой (`network`) случай эффект обрабатывает
 * асинхронно, через `.then`/`.catch` (случай 3).
 */
type BootstrapDecision = { kind: 'signed_out' } | { kind: 'login_failed'; code: string } | { kind: 'network' };

function decideBootstrap(): BootstrapDecision {
  if (window.location.pathname === '/signed-out') {
    return { kind: 'signed_out' };
  }
  const authError = new URLSearchParams(window.location.search).get(AUTH_ERROR_PARAM);
  if (authError) {
    return { kind: 'login_failed', code: authError };
  }
  return { kind: 'network' };
}

function initialState(decision: BootstrapDecision): AuthState {
  switch (decision.kind) {
    case 'signed_out':
      return { status: 'anonymous', reason: 'signed_out', loginFailedCode: null };
    case 'login_failed':
      return { status: 'anonymous', reason: 'login_failed', loginFailedCode: decision.code };
    case 'network':
      return { status: 'initializing' };
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [decision] = useState(decideBootstrap);
  const [state, setState] = useState<AuthState>(() => initialState(decision));
  const queryClient = useQueryClient();
  // true только для самой первой сетевой попытки — по ней панель отличает
  // «первое открытие без сессии» (fresh, автопереход на Keycloak) от потери
  // сессии посреди работы (expired). Последующие
  // успешные и неуспешные попытки (упреждающее продление, повтор на 401)
  // приходят через тот же обработчик уже с этим флагом снятым.
  const isFirstAttemptRef = useRef(decision.kind === 'network');

  const applyToken = useCallback((token: string) => {
    const claims = decodeAccessToken(token);
    isFirstAttemptRef.current = false;
    setState({ status: 'authenticated', displayName: claims.displayName, permissions: claims.permissions });
  }, []);

  const handleSessionError = useCallback((error: unknown) => {
    const wasFirstAttempt = isFirstAttemptRef.current;
    isFirstAttemptRef.current = false;

    if (error instanceof SessionLostError) {
      if (wasFirstAttempt && error.code === 'session.anonymous') {
        // Первое открытие без сессии — не экран, а сразу переход на вход
        // (состояние `fresh`, «не рендерится»).
        sessionSignIn(currentPath());
        return;
      }
      setState({ status: 'anonymous', reason: 'expired', loginFailedCode: null });
      return;
    }
    // AuthUnavailableError и любой прочий отказ — тот же экран (`auth_unavailable`).
    setState({ status: 'anonymous', reason: 'auth_unavailable', loginFailedCode: null });
  }, []);

  useEffect(() => {
    // Один обработчик на все успехи/провалы shared/api/session — независимо
    // от того, что именно попросило токен (загрузка, httpClient на 401,
    // упреждающее продление внутри самого session-модуля).
    setSessionTokenHandler(applyToken);
    setSessionErrorHandler(handleSessionError);

    if (decision.kind === 'login_failed') {
      stripAuthErrorFromUrl();
    }
    if (decision.kind === 'network') {
      getAccessToken().catch(handleSessionError);
    }

    return () => {
      setSessionTokenHandler(null);
      setSessionErrorHandler(null);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- запускается один раз при монтировании; decision вычислено один раз в useState и не меняется
  }, []);

  const retryBootstrap = useCallback(() => {
    setState({ status: 'initializing' });
    getAccessToken().catch(handleSessionError);
  }, [handleSessionError]);

  const signIn = useCallback((returnTo?: string) => {
    sessionSignIn(returnTo ?? currentPath());
  }, []);

  const signOut = useCallback(() => {
    sessionSignOut(() => queryClient.clear()).catch(() => {
      setState({ status: 'anonymous', reason: 'logout_failed', loginFailedCode: null });
    });
  }, [queryClient]);

  const retrySignOut = useCallback(() => {
    signOut();
  }, [signOut]);

  const can = useCallback(
    (permission: string) => (state.status === 'authenticated' ? state.permissions.includes(permission) : false),
    [state],
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      status: state.status,
      reason: state.status === 'anonymous' ? state.reason : null,
      loginFailedCode: state.status === 'anonymous' ? state.loginFailedCode : null,
      displayName: state.status === 'authenticated' ? state.displayName : '',
      can,
      signIn,
      signOut,
      retryBootstrap,
      retrySignOut,
    }),
    [state, can, signIn, signOut, retryBootstrap, retrySignOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth должен вызываться внутри AuthProvider.');
  }
  return context;
}
