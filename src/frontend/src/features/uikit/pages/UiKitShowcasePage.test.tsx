import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { authValue } from '../../../test/renderShell';
import { renderWithProviders } from '../../../test/renderWithProviders';
import { AuthContext } from '../../auth/AuthContext';
import { showcaseRegistry } from '../showcase/registry';
import { uiKitPermissions } from '../permissions';
import { UI_KIT_PATH } from '../paths';
import { UiKitShowcasePage } from './UiKitShowcasePage';

// tech-015 design § 10.1, § 10.6, § 12 (part 8): the page renders under its permission,
// answers «Доступ ограничен» without it, lists its sections, makes no request, and its
// windows and notices open for real.
//
// The page is the whole kit on one screen — about 1,100 elements under about 1,900 CSS rules
// in jsdom, which takes seconds to render. A role query over it resolves every candidate's
// visibility through `getComputedStyle`, which matches every rule against the element and
// its ancestors: measured on this page, the first query after an edit takes about 1.4 s (it
// took about 2.5 s while the kit's fields used `:has` rules, which review R1-2 removed), and
// a repeat of the same query about 60 ms. So the page is read by the DOM (ids, tags,
// attributes), and a role query is made only inside a small subtree.

const SLOW = 60_000;

function renderPage(codes: readonly string[]) {
  return renderWithProviders(<UiKitShowcasePage />, {
    initialEntries: [UI_KIT_PATH],
    wrapper: (children) => (
      <AuthContext.Provider value={authValue(codes)}>{children}</AuthContext.Provider>
    ),
  });
}

const ADMIN = [uiKitPermissions.showcaseRead];

/** Every `.ts` / `.tsx` file under a directory. */
function listSources(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const full = path.join(directory, entry);
    if (statSync(full).isDirectory()) return listSources(full);
    return /\.tsx?$/.test(entry) ? [full] : [];
  });
}

/** The section's wrapper, found by the id the contents link to. */
function sectionWrapper(id: string): HTMLElement {
  const wrapper = document.getElementById(id);
  if (!wrapper) throw new Error(`No section #${id}`);
  return wrapper;
}

/** A button of a demo, by its visible name, without a role query over the whole page. */
function demoButton(scope: HTMLElement, name: string): HTMLElement {
  const button = within(scope).getByText(name).closest('button');
  if (!button) throw new Error(`No button «${name}»`);
  return button;
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('tech-015 Scenario: the showcase page (design § 10)', () => {
  it('gates the page by one existing instance permission (D-5)', () => {
    expect(uiKitPermissions.showcaseRead).toBe('dictionaries.languages.write');
  });

  it('answers «Доступ ограничен» without the code and mounts nothing of the showcase', () => {
    renderPage(['dictionaries.languages.read']);
    expect(screen.getByRole('heading', { level: 1, name: 'Доступ ограничен' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Витрина компонентов' })).not.toBeInTheDocument();
    expect(document.querySelector('section')).toBeNull();
  });

  it(
    'shows the holder of the code one h1, the tab title and every section, and makes no request',
    async () => {
      const fetchSpy = vi.spyOn(globalThis, 'fetch');
      renderPage(ADMIN);

      const h1s = document.querySelectorAll('h1');
      expect(h1s).toHaveLength(1);
      expect(h1s[0]).toHaveTextContent('Витрина компонентов');
      expect(document.title).toBe('Витрина компонентов — Mustask');

      // The contents: a named navigation with a link to each section, in the page's order.
      const contents = screen.getByRole('navigation', { name: 'Содержание витрины' });
      const links = within(contents).getAllByRole('link');
      expect(links.map((link) => link.textContent)).toEqual(
        showcaseRegistry.map((section) => section.title),
      );

      // Each anchor holds one region named by the section's own h2, and its usage sentence.
      showcaseRegistry.forEach((section, index) => {
        expect(links[index]).toHaveAttribute('href', `#${section.id}`);
        const regions = sectionWrapper(section.id).querySelectorAll(':scope > section');
        expect(regions).toHaveLength(1);
        const heading = document.getElementById(regions[0]?.getAttribute('aria-labelledby') ?? '');
        expect(heading?.tagName).toBe('H2');
        expect(heading).toHaveTextContent(section.title);
        expect(within(sectionWrapper(section.id)).getByText(section.usage)).toBeInTheDocument();
      });

      // Every section title is an h2 and no other heading is: the demos' own heads are lower.
      expect(
        Array.from(document.querySelectorAll('h2'))
          .map((heading) => heading.textContent)
          .filter((text) => showcaseRegistry.some((section) => section.title === text)),
      ).toHaveLength(showcaseRegistry.length);

      // The app's HTTP client waits for the access token before it calls `fetch`, so a request
      // made on mount is not there yet when `render` returns: let a macrotask pass, which
      // drains every promise already waiting, and ask then (review R1-9). The static check
      // below is the stronger half: the slice cannot reach the client at all.
      await new Promise((resolve) => setTimeout(resolve, 50));
      expect(fetchSpy).not.toHaveBeenCalled();
    },
    SLOW,
  );

  it('imports nothing that can make a request: no API layer, no HTTP client, no query hook', () => {
    const sliceDirectory = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
    const sources = listSources(sliceDirectory).filter((file) => !/\.test\.tsx?$/.test(file));
    // The slice is read, not assumed empty: the page, the registry and a demo at least.
    expect(sources.length).toBeGreaterThan(10);
    const reaches = /shared\/api|httpClient|@tanstack\/react-query|\bfetch\s*\(|XMLHttpRequest/;
    const offenders = sources.filter((file) => reaches.test(readFileSync(file, 'utf8')));
    expect(offenders.map((file) => path.relative(sliceDirectory, file))).toEqual([]);
  });

  describe('Scenario: the demos are alive', () => {
    it(
      'opens the card’s window from its button and closes it with Escape',
      async () => {
        renderPage(ADMIN);
        fireEvent.click(demoButton(sectionWrapper('dialogs'), 'Открыть окно карточки'));
        await waitFor(() => expect(document.querySelector('[role="dialog"]')).not.toBeNull());
        const dialog = document.querySelector<HTMLElement>('[role="dialog"]');
        expect(dialog).toHaveAccessibleName('Пользовательская история MSK-US-2');
        fireEvent.keyDown(dialog as HTMLElement, { key: 'Escape' });
        await waitFor(() => expect(document.querySelector('[role="dialog"]')).toBeNull());
      },
      SLOW,
    );

    it(
      'shows a notice from its button',
      async () => {
        renderPage(ADMIN);
        fireEvent.click(demoButton(sectionWrapper('notices'), 'Показать уведомление'));
        expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
      },
      SLOW,
    );

    it(
      'turns the card’s Save group on when the title is edited',
      () => {
        renderPage(ADMIN);
        const card = sectionWrapper('card');
        expect(demoButton(card, 'Сохранить')).toBeDisabled();
        fireEvent.change(within(card).getByRole('textbox', { name: 'Заголовок' }), {
          target: { value: 'Письмо со ссылкой!' },
        });
        expect(demoButton(card, 'Сохранить')).toBeEnabled();
      },
      SLOW,
    );
  });
});
