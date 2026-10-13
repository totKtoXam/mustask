import { afterEach, describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import FileDownloadIcon from '@mui/icons-material/FileDownload';
import FileUploadIcon from '@mui/icons-material/FileUpload';
import PrintIcon from '@mui/icons-material/Print';
import { px, theme } from '../../test/kitTesting';
import { PageChromeContext } from './pageChrome';
import { PageHeader, type PageHeaderProps } from './PageHeader';

// tech-015 design § 5.6 «PageHeader»: `headingLevel`, the registration through `usePageHead`,
// the kit's overflow «⋮» and the grey default of a command in the team look; every other page
// keeps the legacy head, unchanged.

function renderHeader(
  props: PageHeaderProps,
  chrome?: Parameters<typeof PageChromeContext.Provider>[0]['value'],
) {
  const tree = (
    <MemoryRouter>
      <PageHeader {...props} />
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

describe('tech-015 Scenario: PageHeader.headingLevel (design § 5.6)', () => {
  it('is the page’s only h1 by default', () => {
    renderHeader({ title: 'Языки' });
    expect(screen.getByRole('heading', { level: 1, name: 'Языки' })).toBeInTheDocument();
  });

  it.each([2, 3] as const)('is an h%s when the head stands inside a region', (level) => {
    renderHeader({ title: 'Образец', headingLevel: level });
    expect(screen.getByRole('heading', { level, name: 'Образец' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1 })).toBeNull();
  });

  it('changes the element and nothing of the look: the page title’s type at every level', () => {
    const { unmount } = renderHeader({ title: 'Один', headingLevel: 1 });
    const one = getComputedStyle(screen.getByRole('heading', { name: 'Один' }));
    const expected = [one.fontSize, one.fontWeight, one.lineHeight];
    unmount();
    renderHeader({ title: 'Три', headingLevel: 3 });
    const three = getComputedStyle(screen.getByRole('heading', { name: 'Три' }));
    expect([three.fontSize, three.fontWeight, three.lineHeight]).toEqual(expected);
    expect(px(three.fontSize)).toBe(20);
  });
});

describe('tech-015 Scenario: the head registers and names the tab through usePageHead (design § 5.6)', () => {
  it('registers its ancestors with the shell once and names the tab', async () => {
    const registerHeader = vi.fn(() => () => {});
    renderHeader(
      { title: 'Добавить язык', ancestors: [{ label: 'Языки', to: '/dictionaries/languages' }] },
      { registerHeader, registerNotFound: () => () => {}, trail: [], trailPlacement: 'topBar' },
    );
    expect(registerHeader).toHaveBeenCalledExactlyOnceWith([
      { label: 'Языки', to: '/dictionaries/languages' },
    ]);
    await vi.waitFor(() => expect(document.title).toBe('Добавить язык — Mustask'));
  });

  it('draws the trail layout.pageHead.trailGap above the title when the shell asks for it there', () => {
    renderHeader(
      { title: 'Языки', variant: 'team' },
      {
        registerHeader: () => () => {},
        registerNotFound: () => () => {},
        trail: [{ label: 'Настройки' }, { label: 'Языки', to: '/dictionaries/languages' }],
        trailPlacement: 'header',
      },
    );
    const trail = screen.getByRole('navigation', { name: 'Путь к странице' });
    const slot = trail.parentElement as HTMLElement;
    expect(px(getComputedStyle(slot).marginBottom)).toBe(theme.layout.pageHead.trailGap);
    expect(theme.layout.pageHead.trailGap).toBe(8);
  });
});

describe('tech-015 Scenario: the team look’s commands and overflow (design § 5.6)', () => {
  const commands = [
    { label: 'Импорт', icon: <FileUploadIcon />, onClick: () => {} },
    { label: 'Экспорт', icon: <FileDownloadIcon />, onClick: () => {} },
    { label: 'Печать', icon: <PrintIcon />, onClick: () => {} },
  ];

  it('makes a command with no tone a grey button in the team look', () => {
    renderHeader({ title: 'Платформа', variant: 'team', commands: [commands[0]!] });
    const button = screen.getByRole('button', { name: 'Импорт' });
    expect(button).toHaveClass('MuiButton-contained');
    expect(button).toHaveClass('MuiButton-colorInherit');
  });

  it('keeps a command with no tone an accent-iconed text button in the legacy head', () => {
    renderHeader({ title: 'Языки', commands: [commands[0]!] });
    const button = screen.getByRole('button', { name: 'Импорт' });
    expect(button).toHaveClass('MuiButton-text');
    expect(button).toHaveClass('MuiButton-colorPrimary');
  });

  function overflowTo(width: number) {
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
    // 10 px per character: «Импорт» 60, «Экспорт» 70, «Печать» 60.
    vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockImplementation(function (
      this: HTMLElement,
    ) {
      return (this.textContent ?? '').length * 10;
    });
    return () =>
      act(() => {
        notify?.([{ contentRect: { width } } as ResizeObserverEntry], {} as ResizeObserver);
      });
  }

  it('moves what does not fit into the kit’s «⋮» — the vertical dots, not the legacy «⋯»', async () => {
    const squeeze = overflowTo(160);
    const onPrint = vi.fn();
    renderHeader({
      title: 'Платформа',
      variant: 'team',
      commands: [commands[0]!, commands[1]!, { ...commands[2]!, onClick: onPrint }],
    });
    squeeze();

    const more = screen.getByRole('button', { name: 'Ещё действия' });
    expect(more.querySelector('[data-testid="MoreHorizIcon"]')).toBeNull();
    expect(more.querySelector('svg')).toHaveAttribute('data-testid', 'ThinMoreVerticalIcon');
    expect(more).toHaveAttribute('aria-haspopup', 'menu');

    await userEvent.click(more);
    const menu = screen.getByRole('menu', { name: 'Ещё действия' });
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Экспорт', 'Печать']);
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Печать' }));
    expect(onPrint).toHaveBeenCalledOnce();
  });

  it('keeps the legacy head’s «⋯» overflow as it was', () => {
    const squeeze = overflowTo(160);
    renderHeader({ title: 'Языки', commands });
    squeeze();

    const more = screen.getByRole('button', { name: 'Ещё действия' });
    expect(more.querySelector('[data-testid="MoreHorizIcon"]')).not.toBeNull();
    expect(more.querySelector('[data-testid="ThinMoreVerticalIcon"]')).toBeNull();
  });
});
