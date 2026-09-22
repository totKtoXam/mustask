// Общие тестовые данные и помощник токена для этого среза — единственный
// источник образцов DTO: каждый тест страницы импортирует отсюда, вместо того
// чтобы заново набирать те же JSON-тела и заново вызывать fakeAccessToken.
import { fakeAccessToken } from '../../test/mocks/handlers';
import type { DictionariesPermission } from './permissions';
import type { LanguageResponse } from './api/dictionariesApi';

/**
 * Токен с ровно перечисленными кодами прав этого среза — тонкая обёртка над
 * `test/mocks/handlers.ts`'s `fakeAccessToken`, чтобы ни один тест страницы
 * не набирал `resource_access` вручную.
 */
export function fakeAccessTokenWithPermissions(codes: DictionariesPermission[] = []): string {
  return fakeAccessToken({ permissions: codes });
}

export const sampleLanguages: LanguageResponse[] = [
  {
    code: 'ru',
    endonym: 'Русский',
    isDefault: true,
    isActive: true,
    sortOrder: 10,
    version: '1001',
    updatedAt: '2026-09-12T07:12:00Z',
    updatedBy: 'admin',
  },
  {
    code: 'kk',
    endonym: 'Қазақша',
    isDefault: false,
    isActive: true,
    sortOrder: 20,
    version: '1002',
    updatedAt: '2026-09-11T07:12:00Z',
    updatedBy: 'admin',
  },
  {
    code: 'tr',
    endonym: 'Türkçe',
    isDefault: false,
    isActive: false,
    sortOrder: 40,
    version: '1003',
    updatedAt: '2026-09-10T07:12:00Z',
    updatedBy: 'admin',
  },
];
