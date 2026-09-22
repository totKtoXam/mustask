import { afterEach, describe, expect, it, vi } from 'vitest';
import { HttpResponse, http } from 'msw';
import { server } from '../../test/mocks/server';
import { fakeAccessToken } from '../../test/mocks/handlers';
import { ApiError, apiFetch } from './httpClient';
import { AuthUnavailableError, resetSessionModule, setSessionErrorHandler } from './session';

const api = 'http://localhost:5000';

function tokenResponse(token: string) {
  return HttpResponse.json({
    accessToken: token,
    expiresAt: new Date(Date.now() + 900_000).toISOString(),
    expiresIn: 900,
  });
}

describe('httpClient', () => {
  afterEach(() => {
    resetSessionModule();
  });

  it('прикладывает Bearer-токен, полученный от session.getAccessToken()', async () => {
    const token = fakeAccessToken();
    server.use(http.post(`${api}/api/session/token`, () => tokenResponse(token)));
    let authorization: string | null = null;
    server.use(
      http.get(`${api}/api/dictionaries/languages`, ({ request }) => {
        authorization = request.headers.get('Authorization');
        return HttpResponse.json([]);
      }),
    );

    await apiFetch('/api/dictionaries/languages');

    expect(authorization).toBe(`Bearer ${token}`);
  });

  it('бизнес-запросы уходят с credentials "omit"', async () => {
    server.use(http.post(`${api}/api/session/token`, () => tokenResponse(fakeAccessToken())));
    let credentials: RequestCredentials | undefined;
    server.use(
      http.get(`${api}/api/dictionaries/languages`, ({ request }) => {
        credentials = request.credentials;
        return HttpResponse.json([]);
      }),
    );

    await apiFetch('/api/dictionaries/languages');

    expect(credentials).toBe('omit');
  });

  it('разбирает ProblemDetails в ApiError с кодом и текстом', async () => {
    server.use(http.post(`${api}/api/session/token`, () => tokenResponse(fakeAccessToken())));
    server.use(
      http.post(`${api}/api/dictionaries/languages`, () =>
        HttpResponse.json(
          {
            status: 409,
            detail: 'Язык с кодом «ru» уже существует.',
            errorCode: 'dictionaries.languages.already_exists',
            params: { code: { value: 'ru', type: 'string' } },
          },
          { status: 409 },
        ),
      ),
    );

    const error = await apiFetch('/api/dictionaries/languages', { method: 'POST', body: {} }).catch(
      (caught: unknown) => caught,
    );

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(409);
    expect((error as ApiError).errorCode).toBe('dictionaries.languages.already_exists');
    expect((error as ApiError).body?.detail).toContain('«ru»');
  });

  it('на 401 продлевает токен один раз и повторяет запрос', async () => {
    let tokenCalls = 0;
    server.use(
      http.post(`${api}/api/session/token`, () => {
        tokenCalls += 1;
        return tokenResponse(fakeAccessToken({ preferredUsername: `user-${tokenCalls}` }));
      }),
    );
    const seenTokens: (string | null)[] = [];
    server.use(
      http.get(`${api}/api/dictionaries/languages`, ({ request }) => {
        const token = request.headers.get('Authorization');
        seenTokens.push(token);
        return seenTokens.length === 1
          ? new HttpResponse(null, { status: 401 })
          : HttpResponse.json([{ code: 'ru' }]);
      }),
    );

    const result = await apiFetch<{ code: string }[]>('/api/dictionaries/languages');

    expect(result).toHaveLength(1);
    expect(tokenCalls).toBe(2);
    expect(seenTokens).toHaveLength(2);
    expect(seenTokens[0]).not.toBe(seenTokens[1]);
  });

  it('401 после успешного продления — ApiError(401) без второй попытки продления', async () => {
    let tokenCalls = 0;
    server.use(
      http.post(`${api}/api/session/token`, () => {
        tokenCalls += 1;
        return tokenResponse(fakeAccessToken());
      }),
    );
    server.use(http.get(`${api}/api/dictionaries/languages`, () => new HttpResponse(null, { status: 401 })));

    const error = await apiFetch('/api/dictionaries/languages').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(401);
    // Один вызов на первичный токен, один — на forceRenew; третьего нет.
    expect(tokenCalls).toBe(2);
  });

  it('SessionLostError на продлении — ApiError(401), обработчик сессии уведомлён', async () => {
    server.use(
      // Первый вызов (первичное прикрепление Bearer) — успех; второй
      // (forceRenew на 401) — сессия истекла.
      http.post(`${api}/api/session/token`, () => tokenResponse(fakeAccessToken()), { once: true }),
      http.post(`${api}/api/session/token`, () =>
        HttpResponse.json({ errorCode: 'session.expired' }, { status: 401 }),
      ),
    );
    server.use(http.get(`${api}/api/dictionaries/languages`, () => new HttpResponse(null, { status: 401 })));
    const onSessionError = vi.fn();
    setSessionErrorHandler(onSessionError);

    const error = await apiFetch('/api/dictionaries/languages').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(401);
    expect(onSessionError).toHaveBeenCalledTimes(1);
  });

  it('forceRenew получает 503 — AuthUnavailableError пробрасывается как есть, обработчик сессии не уведомлён', async () => {
    // Сценарий outage: Keycloak недоступен ровно в момент продления
    // посреди работы. До фикса `send()` подменял любую ошибку
    // `getAccessToken` на `ApiError(401)` и звал `notifySessionError` —
    // AuthContext переводил приложение на AnonymousScreen, теряя всё, что
    // было на экране. Правильный исход: AuthUnavailableError долетает до
    // вызывающего как есть, а AuthContext о ней вообще не узнаёт.
    server.use(
      http.post(`${api}/api/session/token`, () => tokenResponse(fakeAccessToken()), { once: true }),
      http.post(`${api}/api/session/token`, () => new HttpResponse(null, { status: 503 })),
    );
    server.use(http.get(`${api}/api/dictionaries/languages`, () => new HttpResponse(null, { status: 401 })));
    const onSessionError = vi.fn();
    setSessionErrorHandler(onSessionError);

    const error = await apiFetch('/api/dictionaries/languages').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(AuthUnavailableError);
    expect(onSessionError).not.toHaveBeenCalled();
  });

  it('первичный getAccessToken падает сетевой ошибкой — AuthUnavailableError пробрасывается как есть', async () => {
    server.use(http.post(`${api}/api/session/token`, () => HttpResponse.error()));
    const onSessionError = vi.fn();
    setSessionErrorHandler(onSessionError);

    const error = await apiFetch('/api/dictionaries/languages').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(AuthUnavailableError);
    expect(onSessionError).not.toHaveBeenCalled();
  });

  it('пять параллельных запросов, упёршихся в истёкший токен, дают один POST /api/session/token на продление', async () => {
    // Сценарий «одно продление на несколько параллельных
    // запросов одной вкладки»: пять запросов панели, а не пять прямых
    // вызовов getAccessToken() (это отдельно проверяет session.test.ts) — на
    // все пять уходит ровно один POST /api/session/token на продление, а не
    // пять. Ключуем по значению токена, а не по счётчику вызовов бизнес-
    // маршрута: единственный способ отличить «ещё не продлённые» запросы от
    // «уже продлённых», не завязываясь на порядок конкурентных промисов.
    let tokenCalls = 0;
    let firstToken: string | null = null;
    server.use(
      http.post(`${api}/api/session/token`, () => {
        tokenCalls += 1;
        const issued = fakeAccessToken({ preferredUsername: `burst-${tokenCalls}` });
        firstToken ??= issued;
        return tokenResponse(issued);
      }),
    );
    server.use(
      http.get(`${api}/api/dictionaries/languages`, ({ request }) => {
        const authorization = request.headers.get('Authorization');
        if (authorization === `Bearer ${firstToken}`) {
          return new HttpResponse(null, { status: 401 });
        }
        return HttpResponse.json([{ code: 'ru' }]);
      }),
    );

    const results = await Promise.all(
      Array.from({ length: 5 }, () => apiFetch<{ code: string }[]>('/api/dictionaries/languages')),
    );

    expect(results).toHaveLength(5);
    results.forEach((result) => expect(result).toEqual([{ code: 'ru' }]));
    // Один запрос за начальным (холодным) кэшем плюс один запрос на
    // продление после общего для всех пятерых 401 — не пять и не десять.
    expect(tokenCalls).toBe(2);
  });

  it('503/403 — ApiError, getAccessToken на продление не вызывается', async () => {
    let tokenCalls = 0;
    server.use(
      http.post(`${api}/api/session/token`, () => {
        tokenCalls += 1;
        return tokenResponse(fakeAccessToken());
      }),
    );
    server.use(http.get(`${api}/api/dictionaries/languages`, () => new HttpResponse(null, { status: 503 })));

    await apiFetch('/api/dictionaries/languages').catch(() => undefined);

    // Только первичный запрос за токеном — ни одного продления сверх него.
    expect(tokenCalls).toBe(1);
  });
});
