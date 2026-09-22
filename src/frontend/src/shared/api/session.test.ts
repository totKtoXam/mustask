import { afterEach, describe, expect, it, vi } from 'vitest';
import { HttpResponse, http } from 'msw';
import { server } from '../../test/mocks/server';
import { fakeAccessToken } from '../../test/mocks/handlers';
import {
  AuthUnavailableError,
  SessionLostError,
  getAccessToken,
  resetSessionModule,
  setNavigateImpl,
  signIn,
  signOut,
} from './session';

const api = 'http://localhost:5000';

describe('session', () => {
  afterEach(() => {
    resetSessionModule();
  });

  it('single-flight: пять параллельных getAccessToken() при холодном кэше — один запрос', async () => {
    let calls = 0;
    server.use(
      http.post(`${api}/api/session/token`, () => {
        calls += 1;
        return HttpResponse.json({
          accessToken: fakeAccessToken(),
          expiresAt: new Date(Date.now() + 900_000).toISOString(),
          expiresIn: 900,
        });
      }),
    );

    const tokens = await Promise.all([
      getAccessToken(),
      getAccessToken(),
      getAccessToken(),
      getAccessToken(),
      getAccessToken(),
    ]);

    expect(calls).toBe(1);
    expect(new Set(tokens).size).toBe(1);
  });

  it('свежий кэш не бьёт сеть без forceRenew', async () => {
    let calls = 0;
    server.use(
      http.post(`${api}/api/session/token`, () => {
        calls += 1;
        return HttpResponse.json({
          accessToken: fakeAccessToken(),
          expiresAt: new Date(Date.now() + 900_000).toISOString(),
          expiresIn: 900,
        });
      }),
    );

    await getAccessToken();
    await getAccessToken();

    expect(calls).toBe(1);
  });

  it('forceRenew игнорирует свежий кэш и всегда идёт в сеть', async () => {
    let calls = 0;
    server.use(
      http.post(`${api}/api/session/token`, () => {
        calls += 1;
        return HttpResponse.json({
          accessToken: fakeAccessToken(),
          expiresAt: new Date(Date.now() + 900_000).toISOString(),
          expiresIn: 900,
        });
      }),
    );

    await getAccessToken();
    await getAccessToken({ forceRenew: true });

    expect(calls).toBe(2);
  });

  it('session.anonymous → SessionLostError с этим кодом', async () => {
    server.use(
      http.post(`${api}/api/session/token`, () =>
        HttpResponse.json({ errorCode: 'session.anonymous' }, { status: 401 }),
      ),
    );

    const error = await getAccessToken().catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(SessionLostError);
    expect((error as SessionLostError).code).toBe('session.anonymous');
  });

  it('session.expired → SessionLostError с этим кодом', async () => {
    server.use(
      http.post(`${api}/api/session/token`, () =>
        HttpResponse.json({ errorCode: 'session.expired' }, { status: 401 }),
      ),
    );

    const error = await getAccessToken().catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(SessionLostError);
    expect((error as SessionLostError).code).toBe('session.expired');
  });

  it('сетевой сбой/503 → AuthUnavailableError', async () => {
    server.use(
      http.post(`${api}/api/session/token`, () => HttpResponse.json(null, { status: 503 })),
    );

    const error = await getAccessToken().catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(AuthUnavailableError);
  });

  it('часы вкладки спешат на несколько минут — планирование по expiresIn не даёт шторм запросов', async () => {
    // Раньше таймер планировался от абсолютного `expiresAt` сервера против
    // локального `Date.now()`: если часы вкладки уходят вперёд, тот же самый
    // токен на каждом срабатывании выглядит для сервера ещё не готовым к
    // настоящему продлению (валиден дольше 60 с ПО ЕГО часам), сервер
    // отвечает тем же остатком жизни, а локальный пересчёт задержки уходит в
    // 0 — панель бьёт /api/session/token без остановки, пока расхождение не
    // выберется само. Здесь у «сервера» — своё, не сдвинутое
    // время (`serverNow`), а у вкладки — на 5 минут впереди (`skewMs`);
    // сервер отдаёт `expiresIn`, посчитанный по себе, и реально продлевает
    // токен только когда для него самого остаётся ≤ 60 с.
    vi.useFakeTimers();
    try {
      const skewMs = 5 * 60_000;
      const tokenLifetimeMs = 900_000;
      let calls = 0;

      // Часы вкладки уже спешат на 5 минут к моменту первого обращения —
      // `serverNow()` ниже определяется ПОСЛЕ этого сдвига: только тогда оно
      // корректно отражает время «без скидки на спешащие часы».
      vi.setSystemTime(Date.now() + skewMs);

      const serverNow = () => Date.now() - skewMs;
      let issuedAtServerMs = serverNow();

      server.use(
        http.post(`${api}/api/session/token`, () => {
          calls += 1;
          let trueRemainingMs = issuedAtServerMs + tokenLifetimeMs - serverNow();
          if (trueRemainingMs <= 60_000) {
            // Сервер реально продлил — токен снова живёт полный срок.
            issuedAtServerMs = serverNow();
            trueRemainingMs = tokenLifetimeMs;
          }
          return HttpResponse.json({
            accessToken: fakeAccessToken(),
            expiresAt: new Date(serverNow() + trueRemainingMs).toISOString(),
            expiresIn: Math.round(trueRemainingMs / 1000),
          });
        }),
      );

      await getAccessToken();
      expect(calls).toBe(1);

      // Внутри окна ожидания — никаких лишних обращений, даже если бы старое
      // сравнение с абсолютным expiresAt сервера уже увидело в этот момент
      // «просроченный» токен из-за расхождения часов.
      await vi.advanceTimersByTimeAsync(100_000);
      expect(calls).toBe(1);
      await vi.advanceTimersByTimeAsync(100_000);
      expect(calls).toBe(1);

      // Штатное срабатывание — ровно через (900 − 60) с от момента получения
      // токена, не раньше.
      await vi.advanceTimersByTimeAsync(840_000 - 200_000);
      expect(calls).toBe(2);

      // И не штормит дальше: следующее продление — не раньше, чем через тот
      // же интервал, а не сразу следующим тиком.
      await vi.advanceTimersByTimeAsync(1_000);
      expect(calls).toBe(2);
    } finally {
      vi.useRealTimers();
    }
  });

  it('signIn(returnTo) переходит на /api/session/login с returnUrl', () => {
    const navigate = vi.fn();
    setNavigateImpl(navigate);

    signIn('/dictionaries/languages/kk');

    expect(navigate).toHaveBeenCalledWith(
      `${api}/api/session/login?returnUrl=${encodeURIComponent('/dictionaries/languages/kk')}`,
    );
  });

  it('signIn с внешним адресом падает на путь по умолчанию "/"', () => {
    const navigate = vi.fn();
    setNavigateImpl(navigate);

    signIn('//evil.example');

    expect(navigate).toHaveBeenCalledWith(
      `${api}/api/session/login?returnUrl=${encodeURIComponent('/')}`,
    );
  });

  it('signOut: сначала обнуляет кэш и зовёт onBeforeNavigate, затем переходит на logoutUrl', async () => {
    server.use(
      http.post(`${api}/api/session/logout`, () =>
        HttpResponse.json({ logoutUrl: 'http://keycloak.invalid/logout' }),
      ),
    );
    const navigate = vi.fn();
    setNavigateImpl(navigate);
    const onBeforeNavigate = vi.fn();

    await signOut(onBeforeNavigate);

    expect(onBeforeNavigate).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenCalledWith('http://keycloak.invalid/logout');
    // onBeforeNavigate должен быть вызван до навигации (// порядок «cached=null → onBeforeNavigate → navigate»).
    const beforeNavigateOrder = onBeforeNavigate.mock.invocationCallOrder[0];
    const navigateOrder = navigate.mock.invocationCallOrder[0];
    expect(beforeNavigateOrder).toBeDefined();
    expect(navigateOrder).toBeDefined();
    expect(beforeNavigateOrder as number).toBeLessThan(navigateOrder as number);
  });

  it('signOut при сетевой ошибке бросает AuthUnavailableError и не зовёт navigate', async () => {
    server.use(http.post(`${api}/api/session/logout`, () => HttpResponse.error()));
    const navigate = vi.fn();
    setNavigateImpl(navigate);

    await expect(signOut()).rejects.toBeInstanceOf(AuthUnavailableError);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('503 от /api/session/logout — тот же AuthUnavailableError, сессия остаётся живой', async () => {
    // Backend возвращает 503 http.service_unavailable, когда back-channel до
    // Keycloak не прошёл: cookie при этом НЕ снимается — панель
    // должна вести себя так же, как при сетевом сбое (logout_failed,
    // "Повторить выход"), а не терять уже полученный токен: кэш здесь —
    // прокси на «сессия жива», следующий getAccessToken() не обязан идти в
    // сеть заново.
    server.use(
      http.post(`${api}/api/session/token`, () => tokenResponse(fakeAccessToken())),
      http.post(`${api}/api/session/logout`, () => new HttpResponse(null, { status: 503 })),
    );
    const navigate = vi.fn();
    setNavigateImpl(navigate);

    await getAccessToken();

    await expect(signOut()).rejects.toBeInstanceOf(AuthUnavailableError);
    expect(navigate).not.toHaveBeenCalled();

    let tokenCallsAfterFailedLogout = 0;
    server.use(
      http.post(`${api}/api/session/token`, () => {
        tokenCallsAfterFailedLogout += 1;
        return tokenResponse(fakeAccessToken());
      }),
    );
    await getAccessToken();
    expect(tokenCallsAfterFailedLogout).toBe(0);
  });

  it('после 503 от /api/session/logout таймер упреждающего продления не взведён — панель не возвращается в систему сама', async () => {
    // Регрессия: `signOut` бросал исключение
    // раньше `clearProactiveRenewalTimer()`, так что таймер, поставленный
    // изначальным входом, продолжал тикать поверх состояния `logout_failed`.
    // Сценарий здесь — ровно review-сценарий: вход, неудачный выход (Keycloak
    // лежит), время идёт дальше момента, когда таймер сработал бы, и в этот
    // момент /api/session/token уже готов ответить 200 (Keycloak поднялся).
    // Без исправления это привело бы к вызову токен-обработчика и, в
    // AuthContext, к беззвучному возврату из "Выход не завершён" в рабочий
    // экран — а этого не должно происходить без
    // явного действия человека.
    vi.useFakeTimers();
    try {
      let tokenCalls = 0;
      server.use(
        http.post(`${api}/api/session/token`, () => {
          tokenCalls += 1;
          return tokenResponse(fakeAccessToken());
        }),
        http.post(`${api}/api/session/logout`, () => new HttpResponse(null, { status: 503 })),
      );

      await getAccessToken();
      expect(tokenCalls).toBe(1);

      await expect(signOut()).rejects.toBeInstanceOf(AuthUnavailableError);

      // Момент, когда изначальный таймер упреждающего продления сработал бы
      // (expiresIn 900 с − порог 60 с), плюс запас.
      await vi.advanceTimersByTimeAsync(900_000);

      expect(tokenCalls).toBe(1);
    } finally {
      vi.useRealTimers();
    }
  });

  it('НФТ «проверяемость хранения»: ни вход, ни продление, ни выход не пишут ничего похожего на JWT в localStorage/sessionStorage', async () => {
    // «Проверяемость хранения»: ни одно значение localStorage или
    // sessionStorage не имеет формы JWT (три сегмента, разделённых точкой,
    // как у access/id/refresh-токена) и не им является — единственное место,
    // где живёт что-либо из этого, cookie mustask.session, которую этот модуль
    // не видит вовсе (HttpOnly). Гоняем полный цикл — холодный вход,
    // упреждающее продление (forceRenew) и выход — прежде чем проверять.
    const navigate = vi.fn();
    setNavigateImpl(navigate);
    server.use(
      http.post(`${api}/api/session/token`, () => tokenResponse(fakeAccessToken())),
      http.post(`${api}/api/session/logout`, () =>
        HttpResponse.json({ logoutUrl: 'http://keycloak.invalid/logout' }),
      ),
    );

    await getAccessToken();
    await getAccessToken({ forceRenew: true });
    await signOut();

    const jwtLike = /^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*$/;
    for (const storage of [window.localStorage, window.sessionStorage]) {
      expect(storage.length).toBe(0);
      for (let i = 0; i < storage.length; i += 1) {
        const key = storage.key(i)!;
        expect(jwtLike.test(storage.getItem(key) ?? '')).toBe(false);
      }
    }
  });
});

function tokenResponse(token: string) {
  return HttpResponse.json({
    accessToken: token,
    expiresAt: new Date(Date.now() + 900_000).toISOString(),
    expiresIn: 900,
  });
}
