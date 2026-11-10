import { render, screen } from '@testing-library/react';
import { ThemeProvider } from '@mui/material/styles';
import { describe, expect, it } from 'vitest';
import { theme } from '../theme';
import { ProductMark } from './ProductMark';

function renderMark() {
  render(
    <ThemeProvider theme={theme}>
      <ProductMark />
    </ThemeProvider>,
  );
}

describe('ProductMark (feat-055)', () => {
  it('draws the mark and the wordmark «mustask» as decoration', () => {
    renderMark();

    expect(screen.getByTestId('product-mark-icon')).toHaveAttribute('aria-hidden', 'true');
    const wordmark = screen.getByTestId('product-mark-wordmark');
    expect(wordmark).toHaveAttribute('aria-hidden', 'true');
    expect(wordmark).toHaveTextContent('mustask');
  });

  it('colours the shared «t» amber, so the name reads must + task, not must + ask', () => {
    renderMark();

    const [mus, t] = Array.from(
      screen.getByTestId('product-mark-wordmark').children,
    ) as HTMLElement[];
    expect(mus).toHaveTextContent('mus');
    expect(mus).toHaveStyle({ color: theme.palette.shell.logoMust });
    expect(t).toHaveTextContent(/^t$/);
    expect(t).toHaveStyle({ color: theme.palette.shell.logoAccent });
  });
});
