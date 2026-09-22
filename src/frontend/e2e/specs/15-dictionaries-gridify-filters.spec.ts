import { expect, test } from '@playwright/test';
import { signIn } from '../support/auth';

/**
 * The one seam a mocked test structurally cannot see — a filter value typed
 * into a registry's filter box travels through `gridify-client` (ADR-0016)
 * as a real HTTP query string and is parsed by the real backend's Gridify
 * pipeline. Two bugs once lived exactly here: `code==ru` (a hand-rolled
 * `==`) threw a `GridifyFilteringException`, and wrapping a "contains" value
 * in `*…*` (`=*value*`) silently matched zero rows because `=*` is already
 * the Contains operator. Every unit test mocks the HTTP layer (MSW) — none
 * of them ever hands a value to the real parser, so both bugs shipped with a
 * fully green suite.
 *
 * A known residue is deliberately *not* pinned here: the package's own
 * escape covers exactly `(`, `)`, `,`, `|` and `/` — a value beginning with
 * an operator character (`>`) still reaches the server as syntax and 400s,
 * and a trailing `\` is silently swallowed rather than matched literally.
 * Asserting a 400 for either of those would pin today's known-broken residue
 * as the expected behaviour.
 */
test.describe('15. a filter value reaches the real Gridify parser and comes back right', () => {
  test('contains: a value carrying `,` `|` `(` `)` is escaped, not rejected — no match, no error', async ({ page }) => {
    await signIn(page, { returnPath: '/dictionaries/languages' });

    await page.getByLabel('Код', { exact: true }).fill('a,b|(c)');

    await expect(page.getByText('Ничего не найдено')).toBeVisible();
    await expect(page.getByText('Ни один язык не подходит под заданные условия фильтра.')).toBeVisible();
    // The old failure mode was a 400 surfacing as the registry's own
    // ErrorBanner (an MUI `Alert`, role="alert") instead of the empty-result
    // state above — assert its absence explicitly so a regression here fails
    // on this line, not as a harder-to-diagnose text mismatch on the ones
    // above.
    await expect(page.getByRole('alert')).toHaveCount(0);
  });

  test('contains: an ordinary value matches through the real parser (only the seeded "ru" row)', async ({ page }) => {
    await signIn(page, { returnPath: '/dictionaries/languages' });

    await page.getByLabel('Код', { exact: true }).fill('ru');

    const table = page.getByRole('table', { name: 'Реестр языков' });
    await expect(table.getByRole('cell', { name: 'ru', exact: true })).toBeVisible();
    await expect(table.getByRole('cell', { name: 'kk', exact: true })).toHaveCount(0);
    await expect(table.getByRole('cell', { name: 'en', exact: true })).toHaveCount(0);
  });

  test('equality: opening an existing language by code round-trips through `filter=code=<code>`', async ({ page }) => {
    // `getLanguageByCode` (dictionariesApi.ts) builds an equality filter —
    // the correct `code=<code>`, not the once-broken `code==<code>` — exercised
    // here by the edit form's own initial load, not by a filter box: a
    // language's `code` is restricted to BCP-47 shape and cannot carry the
    // adversarial characters the "contains" tests above use, so there is no
    // meaningful escaping case on this path — only "does the real parser
    // accept the operator this client sends."
    await signIn(page, { returnPath: '/dictionaries/languages/ru' });

    await expect(page.getByText('Язык: ru')).toBeVisible();
    await expect(page.getByLabel('Эндоним')).toHaveValue('Русский');
  });
});
