import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { theme } from '../../app/theme';
import { ErrorBoundary } from './ErrorBoundary';

function Bomb(): never {
  throw new Error('boom');
}

/** `#F8F8F8` → `rgb(248, 248, 248)`, the form jsdom's computed style reports. */
function rgb(hex: string): string {
  const digits = hex.replace('#', '');
  const [red, green, blue] = [0, 2, 4].map((offset) =>
    parseInt(digits.slice(offset, offset + 2), 16),
  );
  return `rgb(${red}, ${green}, ${blue})`;
}

describe('US-11 Scenario: The crash screen on the theme', () => {
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it('announces «Что-то пошло не так» as the level-1 heading with «Обновить страницу», on the application background, tab «Mustask»', () => {
    document.title = 'Языки — Mustask';
    render(
      <ErrorBoundary>
        <Bomb />
      </ErrorBoundary>,
    );

    const alert = screen.getByRole('alert');
    expect(
      within(alert).getByRole('heading', { level: 1, name: 'Что-то пошло не так' }),
    ).toBeInTheDocument();
    expect(within(alert).getByTestId('ErrorOutlineOutlinedIcon')).toBeInTheDocument();
    expect(within(alert).getByRole('button', { name: 'Обновить страницу' })).toHaveClass(
      'MuiButton-contained',
    );
    expect(getComputedStyle(alert).backgroundColor).toBe(rgb(theme.palette.background.default));
    expect(document.title).toBe('Mustask');
  });
});
