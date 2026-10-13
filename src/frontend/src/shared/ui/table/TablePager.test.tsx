import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, rgb, theme } from '../../../test/kitTesting';
import { TablePager, type TablePagerProps } from './TablePager';

// tech-015 design § 5.11 «TablePager»: «1–10 из 25» and the two arrows from MUI's `ruRU`, a
// divider rule above, no rows-per-page choice; the page is passed as a value, not an event.

function renderPager(props: Partial<TablePagerProps> = {}) {
  return render(
    <TablePager page={0} rowsPerPage={10} count={25} onPageChange={() => {}} {...props} />,
  );
}

describe('tech-015 Scenario: TablePager (design § 5.11)', () => {
  it('says which rows it shows, of how many: «1–10 из 25»', () => {
    renderPager();
    expect(screen.getByText('1–10 из 25')).toBeInTheDocument();
  });

  it('follows the page: «11–20 из 25», then «21–25 из 25»', () => {
    const { unmount } = renderPager({ page: 1 });
    expect(screen.getByText('11–20 из 25')).toBeInTheDocument();
    unmount();
    renderPager({ page: 2 });
    expect(screen.getByText('21–25 из 25')).toBeInTheDocument();
  });

  it('has two arrows, named in Russian by MUI’s locale', () => {
    renderPager({ page: 1 });
    const buttons = screen.getAllByRole('button');
    expect(buttons).toHaveLength(2);
    for (const button of buttons) {
      expect(button.getAttribute('aria-label')).toMatch(/страниц/i);
    }
  });

  it('draws no rows-per-page choice', () => {
    renderPager();
    expect(screen.queryByRole('combobox')).toBeNull();
    expect(screen.queryByText(/строк на странице/i)).toBeNull();
  });

  it('calls onPageChange with the next page as a value, not an event', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();
    renderPager({ onPageChange });
    await user.click(screen.getByRole('button', { name: /следующ/i }));
    expect(onPageChange).toHaveBeenCalledExactlyOnceWith(1);
  });

  it('calls it with the previous page from the second', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();
    renderPager({ page: 1, onPageChange });
    await user.click(screen.getByRole('button', { name: /предыдущ/i }));
    expect(onPageChange).toHaveBeenCalledExactlyOnceWith(0);
  });

  it('disables the arrow that has nowhere to go: back on the first page, forward on the last', () => {
    const first = renderPager();
    expect(screen.getByRole('button', { name: /предыдущ/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /следующ/i })).toBeEnabled();
    first.unmount();
    renderPager({ page: 2 });
    expect(screen.getByRole('button', { name: /следующ/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /предыдущ/i })).toBeEnabled();
  });

  it('draws a 1 px divider rule above itself', () => {
    const { container } = renderPager();
    const root = container.firstElementChild as HTMLElement;
    const style = getComputedStyle(root);
    expect(px(style.borderTopWidth)).toBe(theme.layout.dividerWidth);
    expect(style.borderTopColor).toBe(rgb(theme.palette.divider));
  });

  it('is a block in the card’s foot, not a table cell', () => {
    const { container } = renderPager();
    expect((container.firstElementChild as HTMLElement).tagName).toBe('DIV');
  });
});
