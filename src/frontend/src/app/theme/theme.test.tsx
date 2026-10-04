import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import AppBar from '@mui/material/AppBar';
import Button from '@mui/material/Button';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TablePagination from '@mui/material/TablePagination';
import TableRow from '@mui/material/TableRow';
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
