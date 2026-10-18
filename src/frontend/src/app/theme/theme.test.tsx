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
import { contrastRatio } from './contrast';
import { theme } from './theme';
import { focusRing } from './tokens';

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

/** A variant's line height in px: MUI keeps it unitless, the size in rem. */
function lineHeightPx(style: { fontSize?: string | number; lineHeight?: string | number }): number {
  return remToPx(style.fontSize) * Number(style.lineHeight);
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
      table.teamColumn +
      table.projectColumn +
      table.effortColumn +
      table.businessValueColumn +
      table.valueAreaColumn;
    const minWidth = fixedColumns + table.titleColumnMin;
    /** A classic vertical scrollbar of the card. */
    const SCROLLBAR = 17;
    /** The content beside the expanded navigation: 1 440 and 1 280 px windows, less the navigation, less the page's side padding. */
    const contentAt = (window: number) => window - theme.layout.navigationWidth - 48;

    it('sums the nine fixed columns to 900 px and the table’s minimum width to 1 300 px (mod-017 design § 6.4: «Команда» is 120; feat-040 design § 5.1: «Проект» is 120 again)', () => {
      expect(table.teamColumn).toBe(120);
      expect(table.projectColumn).toBe(120);
      expect(fixedColumns).toBe(900);
      expect(minWidth).toBe(1300);
    });

    // feat-027 § 2.6's "fits a 1 440 px window" is superseded by mod-017 design § 6.4: with
    // «Команда» the least width is 1 180 px and the table scrolls in its card, as at 1 280 px;
    // feat-040 design § 5.1 grows it to 1 300 px with «Проект».
    it('scrolls sideways inside its card at 1 440 px too, since «Команда» (mod-017 design § 6.4)', () => {
      expect(minWidth).toBeGreaterThan(contentAt(1440) - SCROLLBAR);
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

  describe('Scenario: The backlog’s generic measures sit in the kit’s groups (tech-015 design § 3.4)', () => {
    it('keeps only the page’s own columns and offset in backlogTable', () => {
      expect(Object.keys(theme.layout.backlogTable).sort()).toEqual(
        [
          'hierarchyColumn',
          'orderColumn',
          'typeColumn',
          'titleColumnMin',
          'stateColumn',
          'teamColumn',
          'projectColumn',
          'effortColumn',
          'businessValueColumn',
          'valueAreaColumn',
          'viewportOffset',
        ].sort(),
      );
    });

    it('carries the lifted values over unchanged — the backlog looks as before', () => {
      const { table, iconAction, mark } = theme.layout;
      expect(table).toMatchObject({
        treeIndent: 16,
        separatorHeight: 20,
        gripHitWidth: 16,
        gripDotOffset: 7,
        gripMarkHeight: 20,
      });
      expect(iconAction.chevron).toEqual({ box: 24, glyph: 20 });
      expect(iconAction.row).toEqual({ box: 24, glyph: 16 });
      expect(iconAction.header).toEqual({ box: 24, glyph: 18 });
      expect(mark.typeIconCompact).toBe(16);
    });
  });

  describe('Scenario: The kit’s colours, type, shape and time (tech-015 design § 3.2)', () => {
    const { shell } = theme.palette;

    it('names the card’s greys and the clean disabled split button in the shell palette', () => {
      expect(shell.cardLabel).toBe('#707070');
      expect(shell.sectionRule).toBe('#EAEAEA');
      expect(shell.splitDisabledFill).toBe('#F0F0F0');
      expect(shell.splitDisabledText).toBe('#959595');
      expect(shell.fieldHover).toMatch(/^#[0-9A-F]{6}$/i);
    });

    it('draws a section’s rule lighter than the divider, and the label darker than the hint’s floor', () => {
      const white = theme.palette.background.paper;
      expect(contrastRatio(shell.sectionRule, white)).toBeLessThan(
        contrastRatio(theme.palette.divider, white),
      );
      expect(contrastRatio(shell.cardLabel, white)).toBeGreaterThanOrEqual(4.5);
    });

    it('sets the card’s title at 17 / 24, Regular — a variant of its own, not the 16 px section head', () => {
      const style = theme.typography.cardTitle;
      expect(remToPx(style.fontSize)).toBe(17);
      expect(lineHeightPx(style)).toBeCloseTo(24, 5);
      expect(style.fontWeight).toBe(400);
    });

    it('reads the corner radius and the notice time from one token each', () => {
      expect(theme.shape.borderRadius).toBe(4);
      expect(theme.noticeDurationMs).toBe(6000);
    });

    it('draws a focused outlined input’s frame as thick as the focus ring', () => {
      const root = theme.components?.MuiOutlinedInput?.styleOverrides?.root;
      if (typeof root !== 'function') throw new Error('MuiOutlinedInput.root is not a callback');
      // The callback reads only `theme`; its other props are MUI's own state.
      const rules = root({ theme, ownerState: {} } as never);

      expect(rules).toMatchObject({
        '&.Mui-focused .MuiOutlinedInput-notchedOutline': { borderWidth: focusRing.width },
      });
    });
  });

  describe('Scenario: The kit’s layout groups (tech-015 design § 3.3)', () => {
    const { layout } = theme;

    it('pins the frame, the icon actions, the marks and the spinner', () => {
      expect(layout.frame).toEqual({ thin: 1, thick: 2 });
      expect(layout.iconAction).toEqual({
        row: { box: 24, glyph: 16 },
        chevron: { box: 24, glyph: 20 },
        header: { box: 24, glyph: 18 },
        bar: { box: 32, glyph: 18 },
      });
      expect(layout.mark).toEqual({
        typeIcon: 20,
        typeIconCompact: 16,
        categoryDotField: 10,
        nameGap: 6,
        avatarInline: 24,
        personGlyph: 16,
      });
      expect(layout.inlineSpinner).toBe(16);
      // The shell's two older marks keep their places beside the new group.
      expect(layout.categoryDot).toBe(8);
      expect(layout.avatarSize).toBe(32);
    });

    it('pins the page head and the table', () => {
      expect(layout.pageHead).toEqual({
        top: 28,
        viewsGap: 12,
        bottom: 20,
        titleGap: 10,
        titleIcon: 24,
        trailGap: 8,
        panelTop: 16,
      });
      expect(layout.table).toEqual({
        headerHeight: 40,
        minHeight: 240,
        treeIndent: 16,
        titleLabelInset: 36,
        separatorHeight: 20,
        gripHitWidth: 16,
        gripDotOffset: 7,
        gripMarkHeight: 20,
        gripDot: 2,
        gripPitch: 6,
        draggedOpacity: 0.6,
      });
    });

    it('pins the field, the section and the columns', () => {
      expect(layout.field).toEqual({
        padY: 4,
        box: 28,
        titleBox: 32,
        gap: 2,
        textInset: 6,
        textAreaInset: 8,
        textAreaTop: 15,
        adornmentGap: 19,
        textMinRows: 3,
      });
      expect(layout.section).toEqual({ headGap: 12, bodyTop: 4, gap: 32, inset: 8 });
      expect(layout.columns).toEqual({
        gutter: 12,
        top: 16,
        bottom: 24,
        gap: 16,
        sideWidth: 320,
        threeMin: 1040,
        twoMin: 680,
      });
    });

    it('pins the card', () => {
      expect(layout.card).toEqual({
        dialogShare: 0.9,
        typeBar: 10,
        typeBarCompact: 4,
        headerInset: 18,
        rightInset: 8,
        headerPadTop: 4,
        headerRowGap: 8,
        headerPadBottom: 8,
        actionGap: 11,
        dialogIconInset: 5,
        dialogIconGap: 2,
        bandMinHeight: 76,
        bandPaddingTop: 6,
        bandRow: 32,
        bandRowGap: 4,
        bandLabelWidth: 82,
        bandBlockWidth: 253,
        bandBlockMin: 240,
        bandBlockGap: 32,
        tabStripHeight: 36,
        tabPadX: 10,
        tabIndicatorOffset: 4,
        stackBelow: 720,
      });
    });

    it('makes every icon action a target of at least 24 px (WCAG 2.5.8)', () => {
      for (const size of Object.values(layout.iconAction)) {
        expect(size.box).toBeGreaterThanOrEqual(24);
        expect(size.glyph).toBeLessThanOrEqual(size.box);
      }
    });
  });

  describe('Scenario: The relations that keep the kit’s measures honest (tech-015 design § 3.6)', () => {
    const { layout, density } = theme;
    const { card, field, section, columns, pageHead, iconAction, frame, mark } = layout;

    it('builds the card header 124 px high from its rows and gaps', () => {
      expect(
        card.headerPadTop +
          3 * density.controlHeight +
          2 * card.headerRowGap +
          card.headerPadBottom,
      ).toBe(124);
    });

    it('puts the header’s toolbar where the reference has it — centres 24, 67 and 110 from the right edge', () => {
      const bar = iconAction.bar.box;
      expect(card.rightInset + 2 * bar + 2 * card.actionGap + bar / 2).toBe(110);
      expect(bar + card.actionGap).toBe(43);
      expect(card.rightInset + bar / 2).toBe(24);
      expect(card.rightInset + bar + card.actionGap + bar / 2).toBe(67);
    });

    it('puts the dialog’s two icons where the reference has them — centres 21 and 55 from the right edge', () => {
      const bar = iconAction.bar.box;
      expect(card.dialogIconInset + bar / 2).toBe(21);
      expect(bar + card.dialogIconGap).toBe(34);
      expect(card.dialogIconInset + bar / 2 + bar + card.dialogIconGap).toBe(55);
    });

    it('keeps the band’s rows inside its height and its blocks on the reference’s pitch', () => {
      expect(card.bandMinHeight).toBeGreaterThanOrEqual(
        card.bandPaddingTop + 2 * card.bandRow + card.bandRowGap,
      );
      expect(card.tabStripHeight).toBeLessThanOrEqual(card.bandMinHeight);
      expect(card.bandBlockMin).toBeLessThanOrEqual(card.bandBlockWidth);
      expect(card.bandBlockWidth + card.bandBlockGap).toBe(285);
      expect(card.headerInset + card.bandBlockWidth + card.bandBlockGap).toBe(303);
    });

    it('starts every band value’s text 88 px from its label and centres the state dot 64 px from it', () => {
      expect(card.bandLabelWidth + field.textInset).toBe(88);
      expect(
        card.bandLabelWidth + field.textInset - field.adornmentGap - mark.categoryDotField / 2,
      ).toBe(64);
    });

    it('stands the selected tab’s indicator 2 px thick and 4 px above the strip’s bottom', () => {
      expect(card.tabIndicatorOffset).toBe(4);
      expect(frame.thick).toBe(2);
      expect(card.tabIndicatorOffset + frame.thick).toBeLessThan(card.tabStripHeight);
    });

    it('leaves the first of three columns at least 360 px', () => {
      expect(columns.twoMin).toBeLessThan(columns.threeMin);
      expect(columns.threeMin - 2 * columns.sideWidth - 2 * columns.gap).toBeGreaterThanOrEqual(
        360,
      );
    });

    it('builds the page head 132 px high, and the backlog’s viewport offset from it', () => {
      expect(
        pageHead.top +
          density.controlHeight +
          pageHead.viewsGap +
          density.tabHeight +
          pageHead.bottom,
      ).toBe(132);
      expect(layout.backlogTable.viewportOffset).toBe(layout.topBarHeight + 132 + 24);
      expect(pageHead.trailGap).toBe(8);
      expect(pageHead.panelTop).toBe(16);
    });

    it('builds a field’s box from its text line, and a field 48 px high whether it is empty or filled', () => {
      expect(field.box).toBeCloseTo(lineHeightPx(theme.typography.body1) + 2 * field.padY, 5);
      expect(field.titleBox).toBeCloseTo(
        lineHeightPx(theme.typography.cardTitle) + 2 * field.padY,
        5,
      );
      expect(
        lineHeightPx(theme.typography.caption) + field.gap + field.box + field.gap,
      ).toBeCloseTo(48, 5);
      expect(field.textInset).toBeLessThan(field.textAreaInset);
      expect(mark.personGlyph).toBeLessThanOrEqual(mark.avatarInline);
    });

    it('builds a section’s head, rule and top padding 41 px before its first field', () => {
      expect(
        lineHeightPx(theme.typography.h2) + section.headGap + layout.dividerWidth + section.bodyTop,
      ).toBeCloseTo(41, 5);
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
