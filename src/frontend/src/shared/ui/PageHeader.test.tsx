import { afterEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AddIcon from '@mui/icons-material/Add';
import FileDownloadIcon from '@mui/icons-material/FileDownload';
import FileUploadIcon from '@mui/icons-material/FileUpload';
import PrintIcon from '@mui/icons-material/Print';
import type { ReactNode } from 'react';
import { theme } from '../../app/theme';
import { PageChromeContext, type PageChrome } from './pageChrome';
import { PageHeader } from './PageHeader';

/** `#242424` → `rgb(36, 36, 36)`, the form jsdom's computed style reports. */
function rgb(hex: string): string {
  const digits = hex.replace('#', '');
  const [red, green, blue] = [0, 2, 4].map((offset) =>
    parseInt(digits.slice(offset, offset + 2), 16),
  );
  return `rgb(${red}, ${green}, ${blue})`;
}

function LocationProbe() {
  const { pathname } = useLocation();
  return <div data-testid="location">{pathname}</div>;
}

function renderAt(path: string, ui: ReactNode, chrome?: PageChrome) {
  const tree = (
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route
          path="*"
          element={
            <>
              {ui}
              <LocationProbe />
            </>
          }
        />
      </Routes>
    </MemoryRouter>
  );
  return render(
    chrome ? <PageChromeContext.Provider value={chrome}>{tree}</PageChromeContext.Provider> : tree,
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('US-6 Scenario: Title and command bar', () => {
  it('shows the title as the level-1 heading and the primary action as a filled accent button with a leading icon', () => {
    renderAt(
      '/dictionaries/languages',
      <PageHeader
        title="Языки"
        primaryAction={{ label: 'Добавить язык', icon: <AddIcon />, onClick: () => {} }}
      />,
    );

    const headings = screen.getAllByRole('heading', { level: 1 });
    expect(headings).toHaveLength(1);
    expect(headings[0]).toHaveTextContent('Языки');

    const primary = screen.getByRole('button', { name: 'Добавить язык' });
    expect(primary).toHaveClass('MuiButton-contained');
    expect(primary.querySelector('.MuiButton-startIcon svg')).not.toBeNull();
    expect(getComputedStyle(primary).getPropertyValue('--variant-containedBg')).toBe(
      theme.palette.primary.main,
    );
  });

  it('sets the browser tab to «<title> — Mustask» (BR-6)', () => {
    renderAt('/home', <PageHeader title="Главная" />);

    expect(document.title).toBe('Главная — Mustask');
  });

  it('runs a command given `to` as a link and one given `onClick` as a button', async () => {
    const onClick = vi.fn();
    renderAt(
      '/somewhere',
      <PageHeader
        title="Страница"
        primaryAction={{ label: 'Создать', icon: <AddIcon />, onClick }}
        commands={[{ label: 'Экспорт в CSV', icon: <FileDownloadIcon />, to: '/export' }]}
      />,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Создать' }));
    expect(onClick).toHaveBeenCalledTimes(1);

    await userEvent.click(screen.getByRole('link', { name: 'Экспорт в CSV' }));
    expect(screen.getByTestId('location')).toHaveTextContent('/export');
  });
});

describe('US-6 Scenario: At most one primary action (BR-2)', () => {
  it('renders one filled command and every other one as a text button with a leading icon', () => {
    renderAt(
      '/workitems',
      <PageHeader
        title="Рабочие элементы"
        primaryAction={{ label: 'Новый рабочий элемент', icon: <AddIcon />, onClick: () => {} }}
        commands={[
          { label: 'Импорт рабочих элементов', icon: <FileUploadIcon />, onClick: () => {} },
          { label: 'Экспорт в CSV', icon: <FileDownloadIcon />, onClick: () => {} },
        ]}
      />,
    );

    const buttons = screen.getAllByRole('button');
    expect(
      buttons.filter((button) => button.classList.contains('MuiButton-contained')),
    ).toHaveLength(1);
    for (const name of ['Импорт рабочих элементов', 'Экспорт в CSV']) {
      const command = screen.getByRole('button', { name });
      expect(command).toHaveClass('MuiButton-text');
      expect(command.querySelector('.MuiButton-startIcon svg')).not.toBeNull();
      // The label is neutral; the leading icon carries the accent (design § 2.2).
      expect(getComputedStyle(command).color).toBe(rgb(theme.palette.text.primary));
      expect(getComputedStyle(command.querySelector('.MuiButton-startIcon') as Element).color).toBe(
        rgb(theme.palette.primary.main),
      );
    }
  });

  it('keeps the primary action leftmost in the command bar', () => {
    renderAt(
      '/workitems',
      <PageHeader
        title="Рабочие элементы"
        primaryAction={{ label: 'Новый рабочий элемент', icon: <AddIcon />, onClick: () => {} }}
        commands={[{ label: 'Экспорт в CSV', icon: <FileDownloadIcon />, onClick: () => {} }]}
      />,
    );

    const [first, second] = screen.getAllByRole('button');
    expect(first).toHaveAccessibleName('Новый рабочий элемент');
    expect(second).toHaveAccessibleName('Экспорт в CSV');
  });
});

describe('US-6 Scenario: A reader sees no write command (BR-9)', () => {
  it('keeps the command bar at the control height with no command, so the content does not shift', () => {
    const { container } = renderAt('/dictionaries/languages', <PageHeader title="Языки" />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    const bar = container.querySelector('.PageHeader-commands');
    expect(bar).not.toBeNull();
    expect(getComputedStyle(bar as Element).minHeight).toBe(`${theme.density.controlHeight}px`);
  });
});

describe('US-6 Scenario: Pivot tabs and overflow in the pattern', () => {
  const views = [
    { label: 'Эпики', to: '/boards/epics' },
    { label: 'Фичи', to: '/boards/features' },
    { label: 'Истории', to: '/boards/stories' },
  ];

  it('shows the views as tabs that are addresses, the current one selected', async () => {
    renderAt('/boards/features', <PageHeader title="Доски" views={views} />);

    const tabs = screen.getAllByRole('tab');
    expect(tabs.map((tab) => tab.textContent)).toEqual(['Эпики', 'Фичи', 'Истории']);
    expect(screen.getByRole('tab', { name: 'Фичи' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Эпики' })).toHaveAttribute('href', '/boards/epics');
    expect(screen.getByRole('tablist')).toHaveAccessibleName('Доски');

    await userEvent.click(screen.getByRole('tab', { name: 'Истории' }));

    expect(screen.getByTestId('location')).toHaveTextContent('/boards/stories');
    expect(screen.getByRole('tab', { name: 'Истории' })).toHaveAttribute('aria-selected', 'true');
  });

  it('moves commands that do not fit into «Ещё действия», last first, in their original order; the primary never moves', async () => {
    let notify: ResizeObserverCallback | null = null;
    class StubResizeObserver {
      constructor(callback: ResizeObserverCallback) {
        notify = callback;
      }
      observe() {}
      unobserve() {}
      disconnect() {}
    }
    vi.stubGlobal('ResizeObserver', StubResizeObserver);
    // 10 px per character: «Новый» 50, «Импорт» 60, «Экспорт» 70, «Печать» 60.
    vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockImplementation(function (
      this: HTMLElement,
    ) {
      return (this.textContent ?? '').length * 10;
    });
    const onPrint = vi.fn();

    renderAt(
      '/workitems',
      <PageHeader
        title="Рабочие элементы"
        primaryAction={{ label: 'Новый', icon: <AddIcon />, onClick: () => {} }}
        commands={[
          { label: 'Импорт', icon: <FileUploadIcon />, onClick: () => {} },
          { label: 'Экспорт', icon: <FileDownloadIcon />, onClick: () => {} },
          { label: 'Печать', icon: <PrintIcon />, onClick: onPrint },
        ]}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Ещё действия' })).not.toBeInTheDocument();

    // Gap 8 px: primary 58, commands 68 + 78 + 68; «Ещё действия» 32 + 8.
    // 160 px beside the primary hold «Импорт» and the menu button only.
    act(() => {
      notify?.([{ contentRect: { width: 58 + 160 } } as ResizeObserverEntry], {} as ResizeObserver);
    });

    expect(screen.getByRole('button', { name: 'Новый' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Импорт' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Экспорт' })).not.toBeInTheDocument();

    const more = screen.getByRole('button', { name: 'Ещё действия' });
    await userEvent.click(more);
    const menu = screen.getByRole('menu');
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Экспорт', 'Печать']);

    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Печать' }));
    expect(onPrint).toHaveBeenCalledTimes(1);
  });
});

describe('US-2 Scenario: Page trail (BR-6) — below 1024 px the trail stands in the page header', () => {
  function chrome(trailPlacement: PageChrome['trailPlacement']): PageChrome {
    return {
      registerHeader: () => () => {},
      registerNotFound: () => () => {},
      trail: [
        { label: 'Настройки' },
        { label: 'Справочники' },
        { label: 'Языки', to: '/dictionaries/languages' },
      ],
      trailPlacement,
    };
  }

  it('renders the trail above the title when the shell asks for it in the header', () => {
    renderAt('/dictionaries/languages/new', <PageHeader title="Добавить язык" />, chrome('header'));

    const trail = screen.getByRole('navigation', { name: 'Путь к странице' });
    expect(trail).toHaveTextContent('Настройки/Справочники/Языки');
    expect(within(trail).getByRole('link', { name: 'Языки' })).toHaveAttribute(
      'href',
      '/dictionaries/languages',
    );
    expect(
      trail.compareDocumentPosition(screen.getByRole('heading', { level: 1 })) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it('leaves the trail to the top bar at 1024 px and wider', () => {
    renderAt('/dictionaries/languages/new', <PageHeader title="Добавить язык" />, chrome('topBar'));

    expect(screen.queryByRole('navigation', { name: 'Путь к странице' })).not.toBeInTheDocument();
  });

  it('registers itself with the shell with its own ancestors, once per change, and unregisters on unmount', () => {
    const unregister = vi.fn();
    const registerHeader = vi.fn(() => unregister);
    const value: PageChrome = {
      registerHeader,
      registerNotFound: () => () => {},
      trail: [],
      trailPlacement: 'topBar',
    };

    const { rerender, unmount } = render(
      <PageChromeContext.Provider value={value}>
        <MemoryRouter>
          <PageHeader title="Язык: ru" ancestors={[{ label: 'Раздел', to: '/section' }]} />
        </MemoryRouter>
      </PageChromeContext.Provider>,
    );
    // A new array with the same content does not register again.
    rerender(
      <PageChromeContext.Provider value={value}>
        <MemoryRouter>
          <PageHeader title="Язык: ru" ancestors={[{ label: 'Раздел', to: '/section' }]} />
        </MemoryRouter>
      </PageChromeContext.Provider>,
    );

    expect(registerHeader).toHaveBeenCalledTimes(1);
    expect(registerHeader).toHaveBeenCalledWith([{ label: 'Раздел', to: '/section' }]);

    unmount();
    expect(unregister).toHaveBeenCalledTimes(1);
  });
});
