import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
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

  it('marks the arrow that has nowhere to go aria-disabled, not natively disabled: back on the first page, forward on the last', () => {
    const first = renderPager();
    const back = screen.getByRole('button', { name: /предыдущ/i });
    expect(back).toHaveAttribute('aria-disabled', 'true');
    expect(back).toBeEnabled();
    expect(screen.getByRole('button', { name: /следующ/i })).not.toHaveAttribute('aria-disabled');
    first.unmount();
    renderPager({ page: 2 });
    const forward = screen.getByRole('button', { name: /следующ/i });
    expect(forward).toHaveAttribute('aria-disabled', 'true');
    expect(forward).toBeEnabled();
    expect(screen.getByRole('button', { name: /предыдущ/i })).not.toHaveAttribute('aria-disabled');
  });

  it('keeps focus on the next arrow after paging to the last page; a further click is a no-op (tech-054)', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();
    function Host() {
      const [page, setPage] = useState(1);
      return (
        <TablePager
          page={page}
          rowsPerPage={10}
          count={25}
          onPageChange={(to) => {
            onPageChange(to);
            setPage(to);
          }}
        />
      );
    }
    render(<Host />);
    const next = screen.getByRole('button', { name: /следующ/i });
    await user.tab();
    await user.tab();
    expect(next).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(screen.getByText('21–25 из 25')).toBeInTheDocument();
    expect(next).toHaveAttribute('aria-disabled', 'true');
    // jsdom keeps focus on a natively disabled button, so focus alone cannot fail; not `disabled` is the pin.
    expect(next).toBeEnabled();
    expect(next).toHaveFocus();
    await user.keyboard('{Enter}');
    await user.click(next);
    expect(onPageChange).toHaveBeenCalledExactlyOnceWith(2);
    expect(screen.getByText('21–25 из 25')).toBeInTheDocument();
    expect(next).toHaveFocus();
  });

  it('keeps focus on the previous arrow after paging to the first page; a further click is a no-op (tech-054)', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();
    function Host() {
      const [page, setPage] = useState(1);
      return (
        <TablePager
          page={page}
          rowsPerPage={10}
          count={25}
          onPageChange={(to) => {
            onPageChange(to);
            setPage(to);
          }}
        />
      );
    }
    render(<Host />);
    const back = screen.getByRole('button', { name: /предыдущ/i });
    await user.tab();
    expect(back).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(screen.getByText('1–10 из 25')).toBeInTheDocument();
    expect(back).toHaveAttribute('aria-disabled', 'true');
    // jsdom keeps focus on a natively disabled button, so focus alone cannot fail; not `disabled` is the pin.
    expect(back).toBeEnabled();
    expect(back).toHaveFocus();
    await user.keyboard('{Enter}');
    await user.click(back);
    expect(onPageChange).toHaveBeenCalledExactlyOnceWith(0);
    expect(screen.getByText('1–10 из 25')).toBeInTheDocument();
    expect(back).toHaveFocus();
  });

  it('still pages back when the page is beyond the last page (count shrank)', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();
    renderPager({ page: 5, count: 25, onPageChange });
    await user.click(screen.getByRole('button', { name: /предыдущ/i }));
    expect(onPageChange).toHaveBeenCalledExactlyOnceWith(4);
  });

  it('marks both arrows aria-disabled while `disabled`, even in the middle of the range, and ignores clicks', () => {
    const onPageChange = vi.fn();
    renderPager({ page: 1, disabled: true, onPageChange });
    for (const button of screen.getAllByRole('button')) {
      expect(button).toHaveAttribute('aria-disabled', 'true');
    }
    fireEvent.click(screen.getByRole('button', { name: /следующ/i }));
    expect(onPageChange).not.toHaveBeenCalled();
    expect(screen.getByText('11–20 из 25')).toBeInTheDocument();
  });

  it('keeps keyboard focus on the arrow that started a load (qa D-11)', async () => {
    const user = userEvent.setup();
    function Host() {
      const [loading, setLoading] = useState(false);
      return (
        <TablePager
          page={1}
          rowsPerPage={10}
          count={25}
          disabled={loading}
          onPageChange={() => setLoading(true)}
        />
      );
    }
    render(<Host />);
    const next = screen.getByRole('button', { name: /следующ/i });
    await user.tab();
    await user.tab();
    expect(next).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(next).toHaveAttribute('aria-disabled', 'true');
    expect(next).toHaveFocus();
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
