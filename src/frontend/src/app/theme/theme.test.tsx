import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import AppBar from '@mui/material/AppBar';
import Button from '@mui/material/Button';
import Link from '@mui/material/Link';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import Tab from '@mui/material/Tab';
import TablePagination from '@mui/material/TablePagination';
import TableRow from '@mui/material/TableRow';
import Tabs from '@mui/material/Tabs';
import { ThemeProvider } from '@mui/material/styles';
import type { ReactNode } from 'react';
import { theme } from './theme';

const BR3_MAX_ROW_HEIGHT = 40;

function renderThemed(ui: ReactNode) {
  return render(<ThemeProvider theme={theme}>{ui}</ThemeProvider>);
}

/** `0.875rem` → 14, through the theme's own root font size. */
function remToPx(value: string | number | undefined): number {
  if (typeof value === 'number') return value;
  const match = /^([\d.]+)rem$/.exec(value ?? '');
  if (!match?.[1]) throw new Error(`Not a rem size: ${String(value)}`);
  return Number(match[1]) * theme.typography.htmlFontSize;
}

/** `#242424` → `rgb(36, 36, 36)`, the form jsdom's computed style reports. */
function rgb(hex: string): string {
  const digits = hex.replace('#', '');
  const [red, green, blue] = [0, 2, 4].map((offset) =>
    parseInt(digits.slice(offset, offset + 2), 16),
  );
  return `rgb(${red}, ${green}, ${blue})`;
}

describe('US-1 The panel speaks one visual language — the theme', () => {
  describe('Scenario: Density tokens (BR-3)', () => {
    it('sets the body text to 14 px', () => {
      expect(remToPx(theme.typography.body1.fontSize)).toBe(14);
      expect(remToPx(theme.typography.body2.fontSize)).toBe(14);
    });

    it('sets Segoe UI as the one font, and the system UI fonts after it (feat-027 D-7)', () => {
      const family = theme.typography.fontFamily ?? '';
      expect(family).toBe(
        '"Segoe UI", -apple-system, BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif',
      );
      expect(family).not.toMatch(/roboto/i);
    });

    it('imports no web font: the entry point loads no @fontsource stylesheet', () => {
      const entry = readFileSync(
        path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../main.tsx'),
        'utf8',
      );
      expect(entry).not.toMatch(/@fontsource/);
    });

    it('draws emphasis in Semibold, the face above Regular that Segoe UI has', () => {
      expect(theme.typography.fontWeightMedium).toBe(600);
      for (const variant of [
        theme.typography.h1,
        theme.typography.h2,
        theme.typography.h3,
        theme.typography.subtitle2,
        theme.typography.button,
      ]) {
        expect(variant.fontWeight).toBe(600);
      }
      expect(theme.typography.body1.fontWeight).toBe(400);
    });

    it('keeps the body, the caption and the table cell at Regular and has no 500 left in the scale', () => {
      expect(theme.typography.body2.fontWeight).toBe(400);
      expect(theme.typography.caption.fontWeight).toBe(400);
      expect(theme.typography.tableCell.fontWeight).toBe(400);
      for (const variant of [
        theme.typography.h1,
        theme.typography.h2,
        theme.typography.h3,
        theme.typography.subtitle2,
        theme.typography.button,
        theme.typography.body1,
        theme.typography.body2,
        theme.typography.caption,
        theme.typography.tableCell,
      ]) {
        expect(variant.fontWeight).not.toBe(500);
      }
    });

    it('draws a selected tab Semibold and an unselected one Regular', () => {
      renderThemed(
        <Tabs value="first">
          <Tab label="Первая" value="first" />
          <Tab label="Вторая" value="second" />
        </Tabs>,
      );

      expect(getComputedStyle(screen.getByRole('tab', { name: 'Первая' })).fontWeight).toBe('600');
      expect(getComputedStyle(screen.getByRole('tab', { name: 'Вторая' })).fontWeight).toBe('400');
    });

    it('makes the top bar 48 px high at every width', () => {
      expect(theme.layout.topBarHeight).toBe(48);
      // The toolbar mixin is the bar's height; MUI's own is 56/64 px.
      expect(theme.mixins.toolbar).toEqual({ minHeight: 48 });
    });

    it('keeps a dense table row at most 40 px high', () => {
      expect(theme.density.registryRowHeight).toBeLessThanOrEqual(BR3_MAX_ROW_HEIGHT);

      renderThemed(
        <Table>
          <TableBody>
            <TableRow data-testid="row">
              <TableCell>ru</TableCell>
            </TableRow>
          </TableBody>
        </Table>,
      );

      const height = parseFloat(getComputedStyle(screen.getByTestId('row')).height);
      expect(height).toBe(theme.density.registryRowHeight);
    });
  });

  describe('Scenario: Colour roles (BR-2)', () => {
    it('names the application background, the navigation surface, the content surface, the neutral text and the accent', () => {
      for (const role of [
        theme.palette.background.default,
        theme.palette.shell.navigation,
        theme.palette.background.paper,
        theme.palette.text.primary,
        theme.palette.primary.main,
      ]) {
        expect(role).toMatch(/^#[0-9A-F]{6}$/i);
      }
    });

    it('keeps the navigation surface unequal to the application background', () => {
      expect(theme.palette.shell.navigation).not.toBe(theme.palette.background.default);
    });

    it('puts the top bar on the content surface', () => {
      renderThemed(
        <AppBar position="static" color="inherit" data-testid="bar">
          Mustask
        </AppBar>,
      );

      expect(getComputedStyle(screen.getByTestId('bar')).backgroundColor).toBe(
        rgb(theme.palette.background.paper),
      );
    });

    it('labels the secondary button variants in the neutral text colour', () => {
      renderThemed(
        <>
          <Button variant="text">Текст</Button>
          <Button variant="outlined">Контур</Button>
        </>,
      );

      expect(getComputedStyle(screen.getByRole('button', { name: 'Текст' })).color).toBe(
        rgb(theme.palette.text.primary),
      );
      expect(getComputedStyle(screen.getByRole('button', { name: 'Контур' })).color).toBe(
        rgb(theme.palette.text.primary),
      );
    });

    it('takes the reference’s blue as the accent, with a dark companion (feat-027 D-10)', () => {
      expect(theme.palette.primary.main).toBe('#0078D4');
      expect(theme.palette.primary.dark).toBe('#0C5598');
      expect(theme.palette.primary.contrastText).toBe('#FFFFFF');
    });

    it('draws a link in the accent’s dark companion, because the accent as text is 4.26:1 on the application background; a link that names another colour is left alone', () => {
      renderThemed(
        <>
          <Link href="/a">Ссылка</Link>
          <Link href="/b" color="inherit">
            Простая
          </Link>
        </>,
      );

      expect(getComputedStyle(screen.getByRole('link', { name: 'Ссылка' })).color).toBe(
        rgb(theme.palette.primary.dark),
      );
      expect(getComputedStyle(screen.getByRole('link', { name: 'Простая' })).color).not.toBe(
        rgb(theme.palette.primary.dark),
      );
      expect(getComputedStyle(screen.getByRole('link', { name: 'Ссылка' })).color).not.toBe(
        rgb(theme.palette.primary.main),
      );
    });

    it('keeps the filled primary action in the accent with a white label', () => {
      renderThemed(<Button variant="contained">Добавить</Button>);

      const style = getComputedStyle(screen.getByRole('button', { name: 'Добавить' }));
      // MUI paints the contained variant through its own CSS variables.
      expect(style.getPropertyValue('--variant-containedBg')).toBe(theme.palette.primary.main);
      expect(style.getPropertyValue('--variant-containedColor')).toBe(
        theme.palette.primary.contrastText,
      );
    });
  });

  describe('Scenario: The backlog table’s measures (feat-027 design § 2.3, § 2.6)', () => {
    const table = theme.layout.backlogTable;
    const fixedColumns =
      table.hierarchyColumn +
      table.orderColumn +
      table.typeColumn +
      table.stateColumn +
      table.effortColumn +
      table.businessValueColumn +
      table.valueAreaColumn;
    const minWidth = fixedColumns + table.titleColumnMin;
    /** A classic vertical scrollbar of the card. */
    const SCROLLBAR = 17;
    /** The content beside the expanded navigation: 1 440 and 1 280 px windows, less the navigation, less the page's side padding. */
    const contentAt = (window: number) => window - theme.layout.navigationWidth - 48;

    it('sums the seven fixed columns to 660 px and the table’s minimum width to 1 060 px', () => {
      expect(fixedColumns).toBe(660);
      expect(minWidth).toBe(1060);
    });

    it('fits a 1 440 px window with the navigation expanded, even with the card’s scrollbar', () => {
      expect(minWidth).toBeLessThanOrEqual(contentAt(1440) - SCROLLBAR);
    });

    it('scrolls sideways inside its card at 1 280 px, so the page does not', () => {
      expect(minWidth).toBeGreaterThan(contentAt(1280) - SCROLLBAR);
    });

    it('keeps the rows 36 px, the head’s buttons 32 px and the viewport offset the top bar + head + padding', () => {
      expect(theme.density.registryRowHeight).toBe(36);
      expect(theme.density.controlHeight).toBe(32);
      // The head: 28 + 32 + 12 + 40 + 20 = 132 px.
      const head = 28 + theme.density.controlHeight + 12 + theme.density.tabHeight + 20;
      expect(head).toBe(132);
      expect(table.viewportOffset).toBe(theme.layout.topBarHeight + head + 24);
    });

    it('sets the table’s body text at 13 / 20, between the 12 px header labels and the 14 px body', () => {
      expect(remToPx(theme.typography.tableCell.fontSize)).toBe(13);
      expect(theme.typography.tableCell.lineHeight).toBeCloseTo(20 / 13, 5);
      expect(remToPx(theme.typography.caption.fontSize)).toBe(12);
    });

    it('gives the card a soft glow of its own, not MUI’s elevation 1', () => {
      expect(theme.cardShadow).toMatch(/^0 3px 7px rgba\(/);
      expect(theme.cardShadow).not.toBe(theme.shadows[1]);
    });

    it('names the row «+» grey — the reference’s pale cross, a little darker (D-10) — and the grey button’s two surfaces in the shell palette', () => {
      expect(theme.palette.shell.addAction).toBe('#8A8886');
      expect(theme.palette.shell.buttonNeutral).toBe('#EBEBEB');
      expect(theme.palette.shell.buttonNeutralHover).toBe('#E2E2E2');
    });
  });

  describe('Scenario: The grey secondary button (feat-027 design § 2.5)', () => {
    it('is a filled button in the neutral grey with the neutral label, not the accent', () => {
      renderThemed(
        <Button variant="contained" color="inherit">
          Показать канбан
        </Button>,
      );

      const style = getComputedStyle(screen.getByRole('button', { name: 'Показать канбан' }));
      expect(style.getPropertyValue('--variant-containedBg')).toBe(
        theme.palette.shell.buttonNeutral,
      );
      expect(style.getPropertyValue('--variant-containedColor')).toBe(theme.palette.text.primary);
    });
  });

  describe('Scenario: Russian locale (NFR Localization)', () => {
    it('reads «1–10 из 25» on the first page of 25 rows, 10 per page', () => {
      renderThemed(
        <TablePagination
          component="div"
          count={25}
          page={0}
          rowsPerPage={10}
          rowsPerPageOptions={[]}
          onPageChange={() => {}}
        />,
      );

      expect(screen.getByText('1–10 из 25')).toBeInTheDocument();
    });
  });
});
