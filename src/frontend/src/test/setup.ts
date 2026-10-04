import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll, beforeEach } from 'vitest';
import { cleanup, configure } from '@testing-library/react';
import { resetSessionModule } from '../shared/api/session';
import { server } from './mocks/server';
import { installMatchMedia, resetViewport } from './viewport';

// jsdom has no matchMedia: the stub answers MUI's useMediaQuery for a
// 1440 px window with motion allowed, unless a test sets otherwise
// (src/test/viewport.ts).
installMatchMedia();
beforeEach(() => {
  resetViewport();
});

// Порог ожиданий findBy*/waitFor. Дефолт 1000 ms не покрывает цепочку
// «восстановление сессии → рендер экрана → запрос данных» под параллельным
// прогоном.
configure({ asyncUtilTimeout: 5000 });

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

// globals выключены — автоматический cleanup Testing Library не регистрируется,
// размонтируем вручную (иначе DOM протекает между тестами файла).
afterEach(() => {
  cleanup();
});

// Модульное состояние сессии (кэш токена, single-flight продления,
// обработчик потери сессии, инъекция навигации) не должно протекать между
// тестами: один тест не может «унаследовать» сессию другого. Cookie сессии
// панель не читает и не пишет сама (HttpOnly) — очищать её со стороны
// теста нечего.
afterEach(() => {
  resetSessionModule();
  localStorage.clear();
});
