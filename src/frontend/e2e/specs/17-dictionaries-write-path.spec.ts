import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';
import { getLanguageByCode, replaceLanguage } from '../support/dictionariesApi';
import { SEED_LANGUAGES } from '../support/dictionariesSeed';
import { registryRow, waitForApiResponse } from '../support/dictionariesUi';

/**
 * The dictionaries administration write path itself. Specs 15/16 are
 * deliberately read-only, and so is every mocked unit test — none of them
 * ever sends a `PUT` to the real backend and reads the row back afterward.
 * This spec does: a language edit actually persists and the registry
 * reflects it.
 *
 * Every row this spec touches is restored to
 * `contracts/dictionaries/languages.json`'s exact seeded values in the test's
 * own `finally`, via the API (`dictionariesApi.ts`) — the seeder only ever
 * inserts what is missing and never updates what exists
 * (`DictionarySeeder.cs`, ADR-0021 §5), so an edit left behind here would
 * otherwise survive every future stand restart.
 */
test.describe('17. dictionaries administration — the write path', () => {
  // The language form's *create* path (`POST /languages`) is deliberately
  // not covered here: there is no `DELETE /languages/{code}` — a language
  // this suite created would have no way back and would survive every future
  // stand restart (the seeder only ever inserts what is missing, ADR-0021
  // §5). Only the *edit* path is covered below, which a `PUT` can restore
  // exactly.
  test('a language is edited, saved, and the registry reflects it', async ({ page }) => {
    const seeded = SEED_LANGUAGES.kk;

    await signIn(page, { returnPath: '/dictionaries/languages/kk' });
    await expect(page.getByText('Язык: kk')).toBeVisible();
    await expect(page.getByLabel('Порядок')).toHaveValue(String(seeded.sortOrder));

    try {
      // The typed value in a controlled `TextField` doesn't prove the `PUT`
      // landed — waiting for the mutation's own response is what stops the
      // later `page.goto` from racing (and cancelling) an in-flight request.
      const save = waitForApiResponse(page, 'PUT', '/api/dictionaries/languages/kk');
      await page.getByLabel('Порядок').fill('25');
      await page.getByRole('button', { name: 'Сохранить' }).click();
      expect((await save).ok()).toBe(true);
      await expect(page.getByLabel('Порядок')).toHaveValue('25');

      await page.goto('/dictionaries/languages');
      const table = page.getByRole('table', { name: 'Реестр языков' });
      await expect(registryRow(table, 'kk').locator('td').nth(4)).toHaveText('25');
    } finally {
      const current = await getLanguageByCode(page.request, 'kk');
      if (current && current.sortOrder !== seeded.sortOrder) {
        await replaceLanguage(page.request, 'kk', { endonym: current.endonym, sortOrder: seeded.sortOrder, isActive: current.isActive }, current.version);
      }
      const restored = await getLanguageByCode(page.request, 'kk');
      expect(restored?.sortOrder).toBe(seeded.sortOrder);
      expect(restored?.endonym).toBe(seeded.endonym);
      expect(restored?.isActive).toBe(seeded.isActive);
    }
  });
});
