import { useState, type ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { MemoryRouter, Route } from 'react-router-dom';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderShell } from '../../test/renderShell';
import { setViewportWidth } from '../../test/viewport';
import { PageChromeContext, type PageChrome, type TrailSegment } from './pageChrome';
import { usePageHead, type PageHeadOptions } from './usePageHead';

// tech-015 design § 5.6 «usePageHead»: the seam a page has with the shell — the tab's title, the
// registration of the head keyed by the serialised ancestors, and the ready trail element when
// the shell asks for it above the page. `PageHeader` calls it; a page with no head of its own
// (the work item card) calls it and passes the trail to `PanelShell`.

function Probe(options: PageHeadOptions & { onTrail?: (trail: ReactElement | null) => void }) {
  const { onTrail, ...rest } = options;
  const { trail } = usePageHead(rest);
  onTrail?.(trail);
  return <div data-testid="probe">{trail}</div>;
}

function chrome(overrides: Partial<PageChrome> = {}): PageChrome {
  return {
    registerHeader: vi.fn(() => () => {}),
    registerNotFound: vi.fn(() => () => {}),
    trail: [],
    trailPlacement: 'topBar',
    ...overrides,
  };
}

function withChrome(value: PageChrome, tree: ReactElement) {
  return render(
    <MemoryRouter>
      <PageChromeContext.Provider value={value}>{tree}</PageChromeContext.Provider>
    </MemoryRouter>,
  );
}

const TRAIL: TrailSegment[] = [
  { label: 'Настройки' },
  { label: 'Справочники', to: '/dictionaries' },
  { label: 'Языки', to: '/dictionaries/languages' },
];

describe('tech-015 Scenario: usePageHead (design § 5.6)', () => {
  describe('Scenario: the tab’s title', () => {
    it('names the tab «<title> — Mustask»', async () => {
      withChrome(chrome(), <Probe title="Рабочий элемент MSK-US-2" />);
      await waitFor(() => expect(document.title).toBe('Рабочий элемент MSK-US-2 — Mustask'));
    });

    it('names the tab by `documentTitle` when it says something other than the title', async () => {
      withChrome(chrome(), <Probe title="Платформа" documentTitle="Бэклоги" />);
      await waitFor(() => expect(document.title).toBe('Бэклоги — Mustask'));
    });

    it('leaves the tab alone while there is no title — the page is still loading', async () => {
      document.title = 'Прежняя вкладка';
      withChrome(chrome(), <Probe />);
      await act(async () => {});
      expect(document.title).toBe('Прежняя вкладка');
    });

    it('takes the title when it arrives', async () => {
      document.title = 'Прежняя вкладка';
      function Loading() {
        const [title, setTitle] = useState<string | undefined>(undefined);
        return (
          <>
            <button type="button" onClick={() => setTitle('Рабочий элемент MSK-US-2')}>
              Загружено
            </button>
            <Probe title={title} />
          </>
        );
      }
      const user = userEvent.setup();
      withChrome(chrome(), <Loading />);
      expect(document.title).toBe('Прежняя вкладка');
      await user.click(screen.getByRole('button', { name: 'Загружено' }));
      await waitFor(() => expect(document.title).toBe('Рабочий элемент MSK-US-2 — Mustask'));
    });
  });

  describe('Scenario: the registration with the shell', () => {
    it('registers the head with its ancestors once, and unregisters it on unmount', () => {
      const unregister = vi.fn();
      const registerHeader = vi.fn(() => unregister);
      const { unmount } = withChrome(
        chrome({ registerHeader }),
        <Probe title="MSK-US-2" ancestors={[{ label: 'Бэклоги', to: '/backlog' }]} />,
      );
      expect(registerHeader).toHaveBeenCalledExactlyOnceWith([
        { label: 'Бэклоги', to: '/backlog' },
      ]);
      expect(unregister).not.toHaveBeenCalled();
      unmount();
      expect(unregister).toHaveBeenCalledOnce();
    });

    it('registers with no ancestors when it is given none', () => {
      const registerHeader = vi.fn(() => () => {});
      withChrome(chrome({ registerHeader }), <Probe title="Главная" />);
      expect(registerHeader).toHaveBeenCalledExactlyOnceWith([]);
    });

    it('registers once per ancestors value: a new array of the same segments on every render does not register again', () => {
      const registerHeader = vi.fn(() => () => {});
      const value = chrome({ registerHeader });
      function Page({ tick }: { tick: number }) {
        // A fresh array each render, as a page that builds its ancestors inline does.
        return (
          <Probe title={`Страница ${tick}`} ancestors={[{ label: 'Бэклоги', to: '/backlog' }]} />
        );
      }
      const { rerender } = render(
        <MemoryRouter>
          <PageChromeContext.Provider value={value}>
            <Page tick={1} />
          </PageChromeContext.Provider>
        </MemoryRouter>,
      );
      for (const tick of [2, 3, 4]) {
        rerender(
          <MemoryRouter>
            <PageChromeContext.Provider value={value}>
              <Page tick={tick} />
            </PageChromeContext.Provider>
          </MemoryRouter>,
        );
      }
      expect(registerHeader).toHaveBeenCalledOnce();
    });

    it('registers again when the ancestors change', () => {
      const unregister = vi.fn();
      const registerHeader = vi.fn(() => unregister);
      const value = chrome({ registerHeader });
      const tree = (label: string) => (
        <MemoryRouter>
          <PageChromeContext.Provider value={value}>
            <Probe title="MSK-US-2" ancestors={[{ label }]} />
          </PageChromeContext.Provider>
        </MemoryRouter>
      );
      const { rerender } = render(tree('Бэклоги'));
      rerender(tree('Канбан'));
      expect(registerHeader).toHaveBeenCalledTimes(2);
      expect(registerHeader).toHaveBeenLastCalledWith([{ label: 'Канбан' }]);
      expect(unregister).toHaveBeenCalledOnce();
    });
  });

  describe('Scenario: the trail above the page', () => {
    it('is a ready trail element when the shell asks for it above the page and it is not empty', () => {
      let seen: ReactElement | null = null;
      withChrome(
        chrome({ trail: TRAIL, trailPlacement: 'header' }),
        <Probe title="Языки" onTrail={(trail) => (seen = trail)} />,
      );
      expect(seen).not.toBeNull();
      const nav = screen.getByRole('navigation', { name: 'Путь к странице' });
      expect(nav).toHaveTextContent('Настройки/Справочники/Языки');
      expect(within(nav).getByRole('link', { name: 'Справочники' })).toHaveAttribute(
        'href',
        '/dictionaries',
      );
    });

    it('is null when the top bar draws the trail (1024 px and wider)', () => {
      let seen: ReactElement | null = null;
      withChrome(
        chrome({ trail: TRAIL, trailPlacement: 'topBar' }),
        <Probe title="Языки" onTrail={(trail) => (seen = trail)} />,
      );
      expect(seen).toBeNull();
      expect(screen.queryByRole('navigation')).toBeNull();
    });

    it('is null when the trail is empty, even where the shell asks for it above the page', () => {
      let seen: ReactElement | null = null;
      withChrome(
        chrome({ trail: [], trailPlacement: 'header' }),
        <Probe title="Языки" onTrail={(trail) => (seen = trail)} />,
      );
      expect(seen).toBeNull();
    });

    it('is inert outside the shell: no registration to make, no trail to draw', () => {
      let seen: ReactElement | null | undefined;
      render(
        <MemoryRouter>
          <Probe title="Карточка" onTrail={(trail) => (seen = trail)} />
        </MemoryRouter>,
      );
      expect(seen).toBeNull();
      expect(screen.getByTestId('probe')).toBeEmptyDOMElement();
    });
  });

  describe('Scenario: in the shell', () => {
    function pageRoute(ancestors: TrailSegment[]) {
      return (
        <Route
          path="/dictionaries/languages/new"
          element={<Probe title="Добавить язык" ancestors={ancestors} />}
        />
      );
    }

    it('hands the trail to the page below 1024 px, and the top bar keeps it at 1440 px', async () => {
      setViewportWidth(800);
      const { unmount } = renderShell({
        path: '/dictionaries/languages/new',
        routes: pageRoute([{ label: 'Языки', to: '/dictionaries/languages' }]),
      });
      const probe = await screen.findByTestId('probe');
      const trail = await within(probe).findByRole('navigation', { name: 'Путь к странице' });
      expect(trail).toHaveTextContent('Языки');
      unmount();

      setViewportWidth(1440);
      renderShell({
        path: '/dictionaries/languages/new',
        routes: pageRoute([{ label: 'Языки', to: '/dictionaries/languages' }]),
      });
      expect(await screen.findByTestId('probe')).toBeEmptyDOMElement();
      expect(screen.getByRole('navigation', { name: 'Путь к странице' })).toBeInTheDocument();
    });
  });
});
