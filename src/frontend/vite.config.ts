import { availableParallelism } from 'node:os';
import { fileURLToPath } from 'node:url';
import { configDefaults, defineConfig } from 'vitest/config';
import { loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// https://vite.dev/config/
export default defineConfig(({ command, mode }) => {
  // VITE_API_BASE_URL вшивается на этапе сборки, и прод-бандл без неё
  // «собирается успешно», но безусловно падает при загрузке
  // (shared/config/env.ts бросает при старте) — поэтому валим сборку явно.
  if (command === 'build') {
    const env = loadEnv(mode, process.cwd(), 'VITE_');
    if (!env.VITE_API_BASE_URL) {
      throw new Error(
        'VITE_API_BASE_URL не задан. Задайте переменную окружения перед ' +
          '`vite build` (см. .env.example).',
      );
    }
  }

  return {
    plugins: [react()],
    server: {
      // Порт пришпилен: он внесён в CORS-список Mustask.Api
      // (Cors:AllowedOrigins в appsettings.Development.json); молчаливый
      // сдвиг Vite на соседний порт ломал бы запросы к API загадочными
      // CORS-ошибками. strictPort — чтобы сдвига не было вовсе.
      //
      // PORT уважается ради Aspire: AppHost поднимает это
      // приложение как ресурс и сообщает порт именно так
      // (`WithHttpEndpoint(env: "PORT")`); без чтения переменной Vite слушал
      // бы не тот порт, что проброшен в Aspire dashboard. Вне Aspire переменной нет
      // и остаётся 3000 — тот же дефолт, что в `Ports:Frontend`.
      port: Number(process.env.PORT ?? 3000),
      strictPort: true,
      // Слушать все интерфейсы — иначе стенд не открыть с другой машины.
      // Aspire передаёт Vite только --port и host не настраивает,
      // поэтому это единственное место, где сетевой доступ включается.
      // allowedHosts не нужен: проверка Host в Vite пропускает localhost и
      // любые IP-адреса, а по имени этот стенд не открывают.
      host: true,
    },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      // Every component test renders under the panel's theme: the bare
      // specifier resolves to a wrapper whose `render` adds the app's
      // ThemeProvider (src/test/testingLibrary.tsx). The shared patterns read
      // theme keys MUI's default theme lacks (`layout`, `density`,
      // `palette.shell`), and test files call `render` without a provider —
      // the alias covers them all without editing one (mod-015 plan § 2.5).
      alias: [
        {
          find: /^@testing-library\/react$/,
          replacement: fileURLToPath(new URL('./src/test/testingLibrary.tsx', import.meta.url)),
        },
      ],
      css: true,
      // Playwright owns e2e/**: its specs also end in *.spec.ts, but they
      // need a live stand and real Keycloak (src/frontend/AGENTS.md
      // § E2E test suite) — Vitest must never pick them up.
      exclude: [...configDefaults.exclude, 'e2e/**'],
      // Пороги и кламп воркеров: при параллельном прогоне jsdom-воркеры
      // конкурируют за ядра, и дефолтные таймауты начинают ложно падать.
      testTimeout: 20000,
      maxWorkers: Math.max(1, Math.min(4, availableParallelism() - 1)),
      // Базовый URL API для тестов — совпадает с моками в src/test/mocks/handlers.ts.
      env: {
        VITE_API_BASE_URL: 'http://localhost:5000',
      },
      // localhost — потому что shared/config/env.ts подменяет loopback-адрес
      // API хостом страницы; с посторонним хостом запросы уходили бы не на
      // тот адрес, что ждут моки MSW. https больше не нужен: cookie
      // сессии панель не читает и не пишет сама, а анти-CSRF-cookie
      // (`__Host-mustask_csrf`, требовавшая защищённого origin'а) ушла вместе со
      // старым контуром входа.
      environmentOptions: {
        jsdom: {
          url: 'http://localhost/',
        },
      },
    },
  };
});
