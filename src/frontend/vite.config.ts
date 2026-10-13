import { readFileSync } from 'node:fs';
import { availableParallelism } from 'node:os';
import { fileURLToPath } from 'node:url';
import { gzipSync } from 'node:zlib';
import { configDefaults, defineConfig } from 'vitest/config';
import { loadEnv, searchForWorkspaceRoot, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';

// feat-018 (ADR-0027 decisions 6 and 10). The rich-text vocabulary is imported
// from `contracts/` beside the panel, so the dev server may serve the
// repository root; a deployment build context must include `contracts/`.
const REPOSITORY_ROOT = fileURLToPath(new URL('../..', import.meta.url));

const EMOJI_MODULE = 'virtual:rich-text-emoji';
const EMOJI_RESOLVED = `\0${EMOJI_MODULE}`;
// emojibase-data group ids that never reach the picker: 2 = component (skin
// tones, hair), 9 = flags (shown as letter pairs on Windows, design § Emoji).
const EMOJI_OMITTED_GROUPS = new Set([2, 9]);

interface EmojibaseEntry {
  unicode: string;
  label: string;
  group?: number;
  order?: number;
  tags?: string[];
}

interface EmojibaseGroup {
  key: string;
  message: string;
  order: number;
}

/**
 * `virtual:rich-text-emoji` — the picker's data as one module (ADR-0027
 * decision 4, M1): `[emoji, Russian label, Russian tags, group index]`,
 * flags and the component group omitted, skin tones dropped, ordered by the
 * data set's own order. Used only through a dynamic `import()`, so Rollup
 * makes it a chunk of its own.
 */
function emojiDataPlugin(): Plugin {
  const dataFile = (name: string) =>
    fileURLToPath(new URL(`./node_modules/emojibase-data/ru/${name}`, import.meta.url));

  return {
    name: 'mustask-rich-text-emoji',
    resolveId(id) {
      return id === EMOJI_MODULE ? EMOJI_RESOLVED : undefined;
    },
    load(id) {
      if (id !== EMOJI_RESOLVED) return undefined;

      const entries = JSON.parse(
        readFileSync(dataFile('compact.json'), 'utf8'),
      ) as EmojibaseEntry[];
      const messages = JSON.parse(readFileSync(dataFile('messages.json'), 'utf8')) as {
        groups: EmojibaseGroup[];
      };

      const groups = messages.groups.filter((group) => !EMOJI_OMITTED_GROUPS.has(group.order));
      const index = new Map(groups.map((group, position) => [group.order, position]));

      const items = entries
        .filter((entry) => entry.group !== undefined && index.has(entry.group))
        .sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
        .map((entry) => [
          entry.unicode,
          entry.label,
          (entry.tags ?? []).join(' '),
          index.get(entry.group as number),
        ]);

      return (
        `export const groups = ${JSON.stringify(groups.map((group) => group.message))};\n` +
        `export const emoji = ${JSON.stringify(items)};\n`
      );
    },
  };
}

/**
 * Build only: writes `bundle-report.json` beside the chunks — per chunk its
 * name, entry flags, gzipped size, static and dynamic imports and modules (id
 * and rendered bytes) —
 * which `scripts/check-bundle.mjs` reads against `bundle-budget.json`
 * (plan-frontend § 10.2).
 */
function bundleReportPlugin(): Plugin {
  return {
    name: 'mustask-bundle-report',
    apply: 'build',
    generateBundle(_options, bundle) {
      const chunks = Object.values(bundle).flatMap((output) =>
        output.type === 'chunk'
          ? [
              {
                fileName: output.fileName,
                name: output.name,
                isEntry: output.isEntry,
                isDynamicEntry: output.isDynamicEntry,
                gzipBytes: gzipSync(output.code).length,
                imports: output.imports,
                dynamicImports: output.dynamicImports,
                modules: Object.entries(output.modules).map(([id, module]) => ({
                  id,
                  bytes: module.renderedLength,
                })),
              },
            ]
          : [],
      );

      this.emitFile({
        type: 'asset',
        fileName: 'bundle-report.json',
        source: JSON.stringify({ chunks }, null, 2),
      });
    },
  };
}

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
    plugins: [react(), emojiDataPlugin(), bundleReportPlugin()],
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
      fs: {
        allow: [searchForWorkspaceRoot(process.cwd()), REPOSITORY_ROOT],
      },
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
