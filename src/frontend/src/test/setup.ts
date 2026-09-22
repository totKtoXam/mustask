import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { cleanup, configure } from '@testing-library/react';
import { resetSessionModule } from '../shared/api/session';
import { server } from './mocks/server';

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
