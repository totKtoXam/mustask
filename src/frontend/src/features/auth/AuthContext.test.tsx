import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { HttpResponse, http } from 'msw';
import { server } from '../../test/mocks/server';
import { fakeAccessToken } from '../../test/mocks/handlers';
import { apiFetch } from '../../shared/api/httpClient';
import { resetSessionModule, setNavigateImpl } from '../../shared/api/session';
import { AuthProvider, useAuth } from './AuthContext';

const api = 'http://localhost:5000';

function Probe() {
  const auth = useAuth();
  const [businessError, setBusinessError] = useState('');
  return (
    <div>
      <div data-testid="status">{auth.status}</div>
      <div data-testid="reason">{auth.reason ?? ''}</div>
      <div data-testid="displayName">{auth.displayName}</div>
      <div data-testid="businessError">{businessError}</div>
      <button onClick={() => auth.signOut()}>signOut</button>
      <button
        onClick={() => {
          apiFetch('/api/dictionaries/languages').catch((error: unknown) => {
            setBusinessError(error instanceof Error ? error.name : 'unknown');
          });
        }}
      >
        fetchBusiness
      </button>
    </div>
  );
}

function renderAuth() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <Probe />
      </AuthProvider>
    </QueryClientProvider>,
  );
}

function setUrl(path: string): void {
  window.history.pushState({}, '', path);
}

describe('AuthContext', () => {
  afterEach(() => {
    resetSessionModule();
    setUrl('/');
  });

  it('bootstrap успешен → authenticated, claim-ы разобраны, navigate не вызван', async () => {
    setUrl('/home');
    server.use(
      http.post(`${api}/api/session/token`, () =>
        HttpResponse.json({
          accessToken: fakeAccessToken({ name: 'Иванов Иван' }),
          expiresAt: new Date(Date.now() + 900_000).toISOString(),
          expiresIn: 900,
        }),
      ),
    );
    const navigate = vi.fn();
    setNavigateImpl(navigate);

    renderAuth();

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));
    expect(screen.getByTestId('displayName')).toHaveTextContent('Иванов Иван');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('session.anonymous без authError → переход на /api/session/login один раз', async () => {
    setUrl('/dictionaries/languages/kk');
    server.use(
      http.post(`${api}/api/session/token`, () =>
        HttpResponse.json({ errorCode: 'session.anonymous' }, { status: 401 }),
      ),
    );
    const navigate = vi.fn();
    setNavigateImpl(navigate);

    renderAuth();

    await waitFor(() => expect(navigate).toHaveBeenCalledTimes(1));
    expect(navigate).toHaveBeenCalledWith(
      `${api}/api/session/login?returnUrl=${encodeURIComponent('/dictionaries/languages/kk')}`,
    );
  });

  it('?authError=access_denied → login_failed с текстом по коду, navigate не вызван', async () => {
    setUrl('/home?authError=access_denied');
    const navigate = vi.fn();
    setNavigateImpl(navigate);

    renderAuth();

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('anonymous'));
    expect(screen.getByTestId('reason')).toHaveTextContent('login_failed');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('?authError=... снимается из адресной строки после монтирования — обновление страницы не должно повторно показать ту же ошибку', async () => {
    // "history.replaceState снимает параметр из адресной
    // строки, чтобы обновление страницы не показывало ту же ошибку снова" —
    // без этой правки закрытая пользователем ошибка возвращалась бы на
    // каждый F5, пока он не нажмёт "Войти" ещё раз.
    setUrl('/dictionaries/languages/kk?authError=sign_in_failed&foo=bar');

    renderAuth();

    await waitFor(() => expect(screen.getByTestId('reason')).toHaveTextContent('login_failed'));
    expect(window.location.search).not.toContain('authError');
    // Остальные параметры запроса (если были) не должны исчезать вместе с ним.
    expect(window.location.search).toContain('foo=bar');
    expect(window.location.pathname).toBe('/dictionaries/languages/kk');
  });

  it('/signed-out → signed_out, /api/session/token не вызывается вовсе', async () => {
    setUrl('/signed-out');
    let tokenCalls = 0;
    server.use(
      http.post(`${api}/api/session/token`, () => {
        tokenCalls += 1;
        return HttpResponse.json({
          accessToken: fakeAccessToken(),
          expiresAt: new Date(Date.now() + 900_000).toISOString(),
        });
      }),
    );

    renderAuth();

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('anonymous'));
    expect(screen.getByTestId('reason')).toHaveTextContent('signed_out');
    expect(tokenCalls).toBe(0);
  });

  it('session.expired → expired, без navigate', async () => {
    setUrl('/home');
    server.use(
      http.post(`${api}/api/session/token`, () =>
        HttpResponse.json({ errorCode: 'session.expired' }, { status: 401 }),
      ),
    );
    const navigate = vi.fn();
    setNavigateImpl(navigate);

    renderAuth();

    await waitFor(() => expect(screen.getByTestId('reason')).toHaveTextContent('expired'));
    expect(navigate).not.toHaveBeenCalled();
  });

  it('сетевой сбой при загрузке → auth_unavailable', async () => {
    setUrl('/home');
    server.use(http.post(`${api}/api/session/token`, () => HttpResponse.error()));

    renderAuth();

    await waitFor(() => expect(screen.getByTestId('reason')).toHaveTextContent('auth_unavailable'));
  });

  it('503 на forceRenew посреди работы не переводит authenticated в anonymous', async () => {
    // Сценарий outage: Keycloak недоступен ровно тогда, когда бизнес-
    // запрос упёрся в 401 и попытался продлить токен. До фикса httpClient
    // подменял AuthUnavailableError на потерю сессии и звал
    // notifySessionError — AuthContext переводил панель на AnonymousScreen,
    // хотя сессия жива и упала только сама сеть до Keycloak.
    setUrl('/home');
    server.use(
      http.post(
        `${api}/api/session/token`,
        () =>
          HttpResponse.json({
            accessToken: fakeAccessToken(),
            expiresAt: new Date(Date.now() + 900_000).toISOString(),
            expiresIn: 900,
          }),
        { once: true },
      ),
      http.post(`${api}/api/session/token`, () => new HttpResponse(null, { status: 503 })),
    );
    server.use(http.get(`${api}/api/dictionaries/languages`, () => new HttpResponse(null, { status: 401 })));

    renderAuth();
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));

    screen.getByText('fetchBusiness').click();

    await waitFor(() =>
      expect(screen.getByTestId('businessError')).toHaveTextContent('AuthUnavailableError'),
    );
    expect(screen.getByTestId('status')).toHaveTextContent('authenticated');
    expect(screen.getByTestId('reason')).toHaveTextContent('');
  });

  it('signOut успешен → navigate на logoutUrl', async () => {
    setUrl('/home');
    server.use(
      http.post(`${api}/api/session/token`, () =>
        HttpResponse.json({
          accessToken: fakeAccessToken(),
          expiresAt: new Date(Date.now() + 900_000).toISOString(),
          expiresIn: 900,
        }),
      ),
      http.post(`${api}/api/session/logout`, () =>
        HttpResponse.json({ logoutUrl: 'http://keycloak.invalid/logout' }),
      ),
    );
    const navigate = vi.fn();
    setNavigateImpl(navigate);

    renderAuth();
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));

    screen.getByText('signOut').click();

    await waitFor(() => expect(navigate).toHaveBeenCalledWith('http://keycloak.invalid/logout'));
  });

  it('сбой signOut → logout_failed', async () => {
    setUrl('/home');
    server.use(
      http.post(`${api}/api/session/token`, () =>
        HttpResponse.json({
          accessToken: fakeAccessToken(),
          expiresAt: new Date(Date.now() + 900_000).toISOString(),
          expiresIn: 900,
        }),
      ),
      http.post(`${api}/api/session/logout`, () => HttpResponse.error()),
    );

    renderAuth();
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));

    screen.getByText('signOut').click();

    await waitFor(() => expect(screen.getByTestId('reason')).toHaveTextContent('logout_failed'));
  });
});
