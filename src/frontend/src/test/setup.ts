import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest';

// The files of one worker share a module cache (`isolate: false`,
// vite.config.ts) and a jsdom window (src/test/sharedJsdom.ts). The app's own
// modules must not be shared: a module-level store of one file must not reach
// another. So the registry is cleared before every file. What the packages in
// node_modules hold (MUI, React, Testing Library) is loaded by Node and stays
// cached — that is the cost this configuration saves.
//
// What `vi.resetModules()` does NOT clear: the result of a `vi.mock` factory is
// kept per worker (Vitest drops it between files only under `isolate: true`).
// Two files that mock the same module would get the first file's stub — so
// src/test/mockRegistry.test.ts fails when a module is mocked by two files.
//
// Because of that, nothing from `src/` is imported at the top of this file: a
// static import here would be evaluated before the reset and the test file
// would get a second instance of it. Every hook below imports what it needs
// when it runs, which is after the test file's imports — the same instance the
// test file uses.
vi.resetModules();

beforeAll(async () => {
  // jsdom has no matchMedia: the stub answers MUI's useMediaQuery for a
  // 1440 px window with motion allowed, unless a test sets otherwise
  // (src/test/viewport.ts).
  const { installMatchMedia } = await import('./viewport');
  installMatchMedia();

  // Порог ожиданий findBy*/waitFor. Дефолт 1000 ms не покрывает цепочку
  // «восстановление сессии → рендер экрана → запрос данных» под параллельным
  // прогоном.
  const { configure } = await import('@testing-library/react');
  configure({ asyncUtilTimeout: 5000 });

  const { server } = await import('./mocks/server');
  server.listen({ onUnhandledRequest: 'error' });
});

afterAll(async () => {
  const { server } = await import('./mocks/server');
  server.close();
});

beforeEach(async () => {
  const { resetViewport } = await import('./viewport');
  resetViewport();
});

// globals выключены — автоматический cleanup Testing Library не регистрируется,
// размонтируем вручную (иначе DOM протекает между тестами файла).
afterEach(async () => {
  const { cleanup } = await import('@testing-library/react');
  cleanup();

  const { server } = await import('./mocks/server');
  server.resetHandlers();

  // After the unmount, so nothing re-inserts a rule into a flushed sheet.
  const { resetStyleCache } = await import('./styleCache');
  resetStyleCache();

  // Модульное состояние сессии (кэш токена, single-flight продления,
  // обработчик потери сессии, инъекция навигации) не должно протекать между
  // тестами: один тест не может «унаследовать» сессию другого. Cookie сессии
  // панель не читает и не пишет сама (HttpOnly) — очищать её со стороны
  // теста нечего.
  const { resetSessionModule } = await import('../shared/api/session');
  resetSessionModule();
  localStorage.clear();

  // What a test spied on or stubbed must not outlive it: the window and the
  // globals are shared by the files of a worker, and `--sequence.shuffle`
  // runs tests of one file in any order (a spy on `navigator.clipboard`
  // from one test reached the next).
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();

  // And what lives on the shared window: fake timers patch the globals,
  // `sessionStorage` and the address outlive a test (and a file).
  vi.useRealTimers();
  sessionStorage.clear();
  window.history.replaceState(null, '', '/');
});
