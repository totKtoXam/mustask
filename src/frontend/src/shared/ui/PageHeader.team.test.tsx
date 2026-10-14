import { describe, expect, it } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { render, screen, waitFor, within } from '@testing-library/react';
import AddIcon from '@mui/icons-material/Add';
import ArrowCircleRightOutlinedIcon from '@mui/icons-material/ArrowCircleRightOutlined';
import { theme } from '../../app/theme';
import { PageHeader, type PageHeaderProps } from './PageHeader';

// feat-027 design § 3.5: what the shared header gains for the team head —
// every addition optional, every other page unchanged.

function renderHeader(props: PageHeaderProps) {
  return render(
    <MemoryRouter initialEntries={['/backlog']}>
      <PageHeader {...props} />
    </MemoryRouter>,
  );
}

const views = [{ label: 'Бэклог', to: '/backlog' }];
// A plain header draws a tab strip for two views or more (mod-016 DC-2).
const twoViews = [
  { label: 'Бэклог', to: '/backlog' },
  { label: 'Архив', to: '/archive' },
];

describe('feat-027 design § 3.5 Scenario: The browser tab can differ from the heading', () => {
  it('reads «<documentTitle> — Mustask» while the h1 is the title', async () => {
    renderHeader({ title: 'Платформа', documentTitle: 'Бэклоги' });

    expect(screen.getByRole('heading', { level: 1, name: 'Платформа' })).toBeInTheDocument();
    await waitFor(() => expect(document.title).toBe('Бэклоги — Mustask'));
  });

  it('reads the title when no documentTitle is given, as before', async () => {
    renderHeader({ title: 'Языки' });

    await waitFor(() => expect(document.title).toBe('Языки — Mustask'));
  });
});

describe('feat-027 design § 3.5 Scenario: A leading icon and a control stand with the h1', () => {
  it('draws the icon, the h1 and the control in that order in one row', () => {
    renderHeader({
      title: 'Платформа',
      titleIcon: <span data-testid="lead" />,
      titleControl: <button type="button">Сменить команду</button>,
    });

    const heading = screen.getByRole('heading', { level: 1, name: 'Платформа' });
    const lead = screen.getByTestId('lead');
    const control = screen.getByRole('button', { name: 'Сменить команду' });
    expect(heading.parentElement).toBe(lead.parentElement);
    expect(heading.parentElement).toBe(control.parentElement);
    expect(lead.compareDocumentPosition(heading) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(
      heading.compareDocumentPosition(control) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });
});

describe('feat-027 design § 3.5 Scenario: The team variant’s spacing', () => {
  it('stands 28 px under the top bar, 20 px above the content, with the tab row 12 px under the first row and a 10 px title gap', () => {
    const { container } = renderHeader({
      title: 'Платформа',
      titleIcon: <span data-testid="lead" />,
      variant: 'team',
      views,
      viewsEnd: <span>Эпики</span>,
    });

    const root = container.querySelector('.PageHeader-root') as HTMLElement;
    const style = getComputedStyle(root);
    expect(style.paddingTop).toBe(theme.spacing(3.5));
    expect(style.paddingBottom).toBe(theme.spacing(2.5));
    expect(parseFloat(style.paddingTop)).toBe(28);
    expect(parseFloat(style.paddingBottom)).toBe(20);
    const group = screen.getByRole('heading', { level: 1 }).parentElement as HTMLElement;
    expect(parseFloat(getComputedStyle(group).gap)).toBe(10);
    const tabRow = screen.getByRole('tablist').closest('.MuiTabs-root')
      ?.parentElement as HTMLElement;
    expect(parseFloat(getComputedStyle(tabRow).marginTop)).toBe(12);
  });

  it('makes the team tab 40 px tall with its label 8 px above the underline — 16 px over the 17.5 px line and 6 px under — not the default 12 + 12, which drew 42 px and a head 2 px taller than the card’s offset assumes (V-2)', () => {
    renderHeader({ title: 'Платформа', variant: 'team', views });

    const tab = screen.getByRole('tab', { name: 'Бэклог' });
    const style = getComputedStyle(tab);
    // jsdom reports the theme's own units: `0.875rem` and a unitless `1.25`.
    const fontSize = parseFloat(style.fontSize) * theme.typography.htmlFontSize;
    const line = fontSize * parseFloat(style.lineHeight);
    expect(fontSize).toBe(14);
    expect(line).toBe(17.5);
    expect(parseFloat(style.paddingTop)).toBe(16);
    expect(parseFloat(style.paddingBottom)).toBe(6);
    // The three add up to 39.5 px; the tab's minimum height rounds the box to the theme's 40.
    expect(
      parseFloat(style.paddingTop) + line + parseFloat(style.paddingBottom),
    ).toBeLessThanOrEqual(theme.density.tabHeight);
    expect(Math.round(parseFloat(style.paddingTop) + line + parseFloat(style.paddingBottom))).toBe(
      theme.density.tabHeight,
    );
    expect(parseFloat(style.minHeight)).toBe(theme.density.tabHeight);
    // The head is then the 132 px the table's `viewportOffset` is built on.
    expect(theme.layout.backlogTable.viewportOffset).toBe(
      theme.layout.topBarHeight +
        28 +
        theme.density.controlHeight +
        12 +
        theme.density.tabHeight +
        20 +
        24,
    );
  });

  it('leaves the other pages’ tabs on the theme’s own padding', () => {
    renderHeader({ title: 'Языки', views: twoViews });

    const style = getComputedStyle(screen.getByRole('tab', { name: 'Бэклог' }));
    expect(parseFloat(style.paddingTop)).toBe(12);
    expect(parseFloat(style.paddingBottom)).toBe(12);
  });

  it('leaves the other pages at 16 px above, 8 px below and an 8 px gap before the tabs', () => {
    const { container } = renderHeader({ title: 'Языки', views: twoViews });

    const style = getComputedStyle(container.querySelector('.PageHeader-root') as HTMLElement);
    expect(parseFloat(style.paddingTop)).toBe(16);
    expect(parseFloat(style.paddingBottom)).toBe(8);
    const tabs = screen.getByRole('tablist').closest('.MuiTabs-root') as HTMLElement;
    expect(parseFloat(getComputedStyle(tabs).marginTop)).toBe(8);
  });

  it('puts one tab at the left of the row and viewsEnd at its right end, flush with the content’s left edge', () => {
    renderHeader({
      title: 'Платформа',
      variant: 'team',
      views,
      viewsLabel: 'Вид страницы',
      viewsEnd: <button type="button">Эпики</button>,
    });

    const list = screen.getByRole('tablist', { name: 'Вид страницы' });
    const tab = within(list).getByRole('tab', { name: 'Бэклог' });
    expect(tab).toHaveAttribute('aria-selected', 'true');
    expect(within(list).getAllByRole('tab')).toHaveLength(1);
    const row = list.closest('.MuiTabs-root')?.parentElement as HTMLElement;
    expect(getComputedStyle(row).justifyContent).toBe('space-between');
    expect(row.lastElementChild).toBe(screen.getByRole('button', { name: 'Эпики' }));
    expect(parseFloat(getComputedStyle(tab).paddingLeft)).toBe(0);
    expect(parseFloat(getComputedStyle(tab).minWidth)).toBe(0);
  });
});

describe('feat-027 design § 3.5 Scenario: A command without a drawn chevron', () => {
  it('keeps aria-haspopup and aria-expanded and draws no trailing ExpandMore', () => {
    renderHeader({
      title: 'Платформа',
      primaryAction: {
        label: 'Новый рабочий элемент',
        icon: <AddIcon />,
        menuOpen: false,
        hideMenuIndicator: true,
        onClick: () => {},
      },
    });

    const button = screen.getByRole('button', { name: 'Новый рабочий элемент' });
    expect(button).toHaveAttribute('aria-haspopup', 'menu');
    expect(button).toHaveAttribute('aria-expanded', 'false');
    expect(button.querySelector('[data-testid="ExpandMoreIcon"]')).toBeNull();
    expect(button.querySelector('[data-testid="AddIcon"]')).not.toBeNull();
  });
});

describe('feat-027 design § 3.5 Scenario: A neutral command is the grey button', () => {
  it('draws a link command with tone neutral as a filled grey button with its icon, the primary staying the one accent', () => {
    renderHeader({
      title: 'Платформа',
      primaryAction: { label: 'Новый рабочий элемент', icon: <AddIcon />, onClick: () => {} },
      commands: [
        {
          label: 'Показать канбан',
          icon: <ArrowCircleRightOutlinedIcon />,
          to: '/board',
          tone: 'neutral',
        },
      ],
    });

    const link = screen.getByRole('link', { name: 'Показать канбан' });
    const grey = getComputedStyle(link);
    expect(grey.getPropertyValue('--variant-containedBg')).toBe(theme.palette.shell.buttonNeutral);
    expect(grey.getPropertyValue('--variant-containedColor')).toBe(theme.palette.text.primary);
    expect(link.querySelector('[data-testid="ArrowCircleRightOutlinedIcon"]')).not.toBeNull();
    const primary = getComputedStyle(screen.getByRole('button', { name: 'Новый рабочий элемент' }));
    expect(primary.getPropertyValue('--variant-containedBg')).toBe(theme.palette.primary.main);
  });

  it('leaves a command without a tone a text button', () => {
    renderHeader({
      title: 'Языки',
      commands: [{ label: 'Печать', icon: <AddIcon />, to: '/print' }],
    });

    const link = screen.getByRole('link', { name: 'Печать' });
    expect(getComputedStyle(link).getPropertyValue('--variant-containedBg')).not.toBe(
      theme.palette.shell.buttonNeutral,
    );
    expect(getComputedStyle(link).getPropertyValue('--variant-textColor')).toBe(
      theme.palette.text.primary,
    );
  });
});
