import type { MouseEvent, ReactElement } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { rgb, theme } from '../../../test/kitTesting';
import { SearchOption } from './SearchOption';

// feat-070 / feat-065 design § 1, § 3.1: one option of the panel's listbox.

function row(ui: ReactElement) {
  return render(
    <MemoryRouter>
      <ul role="listbox" aria-label="Результаты поиска">
        {ui}
      </ul>
    </MemoryRouter>,
  );
}

describe('feat-070 Scenario: SearchOption (feat-065 design § 1)', () => {
  it('is an option and a real link that is never a Tab stop', () => {
    row(<SearchOption id="o1" to="/p/1" primary="Оплата картой" secondary="В работе · Альфа" />);
    const option = screen.getByRole('option', { name: /Оплата картой/ });
    expect(option.tagName).toBe('A');
    expect(option).toHaveAttribute('href', '/p/1');
    expect(option).toHaveAttribute('tabindex', '-1');
    expect(option).toHaveAttribute('id', 'o1');
    expect(screen.getByText('В работе · Альфа')).toBeInTheDocument();
  });

  it('puts aria-selected, the tint, the edge and ↵ on the active row only', () => {
    row(
      <>
        <SearchOption id="a" to="/a" primary="Первая" active />
        <SearchOption id="b" to="/b" primary="Вторая" />
      </>,
    );
    const [first, second] = screen.getAllByRole('option') as [HTMLElement, HTMLElement];
    expect(first).toHaveAttribute('aria-selected', 'true');
    expect(second).toHaveAttribute('aria-selected', 'false');
    const style = getComputedStyle(first);
    expect(style.backgroundColor).toBe(rgb(theme.palette.shell.rowSelected));
    expect(style.borderLeftColor).toBe(rgb(theme.palette.primary.main));
    expect(style.minHeight).toBe(`${theme.layout.searchOption.height}px`);
    const stripe = first.querySelector('[aria-hidden]') as HTMLElement;
    expect(getComputedStyle(stripe).height).toBe(`${theme.layout.searchOption.stripeHeight}px`);
    const hint = (el: HTMLElement) => el.querySelector('kbd') as HTMLElement;
    expect(getComputedStyle(hint(first)).visibility).toBe('visible');
    expect(getComputedStyle(hint(second)).visibility).toBe('hidden');
    expect(hint(first)).toHaveAttribute('aria-hidden', 'true');
  });

  it('draws the stripe in the colour given, and none without one', () => {
    row(
      <>
        <SearchOption id="a" to="/a" primary="Отдел" stripe="#112233" />
        <SearchOption id="b" to="/b" primary="Задача" />
      </>,
    );
    const [first, second] = screen.getAllByRole('option') as [HTMLElement, HTMLElement];
    const stripe = first.querySelector('[data-stripe]');
    expect(stripe).not.toBeNull();
    expect(getComputedStyle(stripe as Element).backgroundColor).toBe(rgb('#112233'));
    expect(second.querySelector('[data-stripe]')).toBeNull();
  });

  it('keeps the whole text in the title and cuts each line with an ellipsis', () => {
    row(
      <SearchOption
        id="a"
        to="/a"
        primary="Очень длинный заголовок"
        title="Очень длинный заголовок полностью"
      />,
    );
    expect(screen.getByRole('option')).toHaveAttribute(
      'title',
      'Очень длинный заголовок полностью',
    );
    expect(getComputedStyle(screen.getByText('Очень длинный заголовок')).textOverflow).toBe(
      'ellipsis',
    );
  });

  it('reports a click and a pointer entering the row', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn((event: MouseEvent) => event.preventDefault());
    const onPointerEnter = vi.fn();
    row(
      <SearchOption
        id="a"
        to="/a"
        primary="Строка"
        onClick={onClick}
        onPointerEnter={onPointerEnter}
      />,
    );
    await user.hover(screen.getByRole('option'));
    expect(onPointerEnter).toHaveBeenCalledOnce();
    await user.click(screen.getByRole('option'));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('the more variant is a plain option with no link', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    row(
      <SearchOption id="m" variant="more" to="/ignored" primary="Показать ещё" onClick={onClick} />,
    );
    const option = screen.getByRole('option', { name: /Показать ещё/ });
    expect(option.tagName).not.toBe('A');
    expect(option).not.toHaveAttribute('href');
    expect(option).toHaveAttribute('tabindex', '-1');
    expect(option.querySelector('[data-stripe]')).toBeNull();
    await user.click(option);
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('is wrapped in a presentational list item, so the listbox holds options only', () => {
    row(<SearchOption id="a" to="/a" primary="Строка" />);
    expect(screen.getByRole('option').parentElement).toHaveAttribute('role', 'presentation');
  });
});
