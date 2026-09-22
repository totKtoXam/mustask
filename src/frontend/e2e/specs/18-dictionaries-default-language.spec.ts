import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';
import { setDefaultLanguage } from '../support/dictionariesApi';
import { registryRow } from '../support/dictionariesUi';

/**
 * Reassigning the default language against the real backend: the registry's
 * row action, the confirmation dialog and the server's own answer — not a
 * fabricated `200` from MSW.
 */
test.describe('18. reassigning the default language', () => {
  test('the default language is reassigned and reassigned back', async ({ page }) => {
    await signIn(page, { returnPath: '/dictionaries/languages' });
    const table = page.getByRole('table', { name: 'Реестр языков' });

    await expect(registryRow(table, 'ru').locator('td').nth(2)).toContainText('да');
    await expect(registryRow(table, 'kk').locator('td').nth(2)).toContainText('нет');

    try {
      await page.getByRole('button', { name: 'Сделать языком по умолчанию kk' }).click();
      await expect(page.getByRole('dialog')).toContainText('Сделать «Қазақша» (kk) языком по умолчанию?');
      await page.getByRole('button', { name: 'Назначить' }).click();
      await expect(page.getByText('Язык по умолчанию изменён на «Қазақша» (kk).')).toBeVisible();

      await expect(registryRow(table, 'kk').locator('td').nth(2)).toContainText('да');
      await expect(registryRow(table, 'ru').locator('td').nth(2)).toContainText('нет');

      await page.getByRole('button', { name: 'Сделать языком по умолчанию ru' }).click();
      await expect(page.getByRole('dialog')).toContainText('Сделать «Русский» (ru) языком по умолчанию?');
      await page.getByRole('button', { name: 'Назначить' }).click();
      await expect(page.getByText('Язык по умолчанию изменён на «Русский» (ru).')).toBeVisible();

      await expect(registryRow(table, 'ru').locator('td').nth(2)).toContainText('да');
      await expect(registryRow(table, 'kk').locator('td').nth(2)).toContainText('нет');
    } finally {
      // Whatever this test did or didn't finish, the default must end up
      // back on `ru` — the seed's own default
      // (contracts/dictionaries/languages.json).
      await setDefaultLanguage(page.request, 'ru');
    }
  });
});
