import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableRow from '@mui/material/TableRow';
import { theme } from '../../../app/theme';
import type { StateCategory } from '../api/processApi';
import { CATEGORY_MARKER_CLASS, CategoryCell } from './CategoryCell';

/** `#A4262C` → `rgb(164, 38, 44)`, the form jsdom's computed style reports. */
function rgb(hex: string): string {
  const digits = hex.replace('#', '');
  const [red, green, blue] = [0, 2, 4].map((offset) =>
    parseInt(digits.slice(offset, offset + 2), 16),
  );
  return `rgb(${red}, ${green}, ${blue})`;
}

function renderCell(category: StateCategory) {
  const { container } = render(
    <Table>
      <TableBody>
        <TableRow>
          <CategoryCell category={category} />
        </TableRow>
      </TableBody>
    </Table>,
  );
  return container.querySelector(`.${CATEGORY_MARKER_CLASS}`) as HTMLElement;
}

describe('CategoryCell — the category label beside its theme colour (design point 6)', () => {
  it('shows the label exactly as the server sent it, and never the code', () => {
    renderCell({ code: 'InProgress', label: 'В работе' });

    expect(screen.getByRole('cell')).toHaveTextContent(/^В работе$/);
    expect(screen.queryByText(/InProgress/)).not.toBeInTheDocument();
  });

  it('holds no map of its own: a label the server words differently is shown as sent', () => {
    renderCell({ code: 'Proposed', label: 'Предложенные' });

    expect(screen.getByRole('cell')).toHaveTextContent(/^Предложенные$/);
    expect(screen.queryByText('Новые')).not.toBeInTheDocument();
  });

  it.each(['Proposed', 'InProgress', 'Resolved', 'Completed'] as const)(
    'fills the hidden marker with categoryColour(%s)',
    (code) => {
      const marker = renderCell({ code, label: 'Метка' });

      expect(marker).toHaveAttribute('aria-hidden', 'true');
      expect(getComputedStyle(marker).backgroundColor).toBe(
        rgb(theme.workTracking.categoryColour(code)),
      );
      expect(getComputedStyle(marker).borderRadius).toBe('50%');
      expect(getComputedStyle(marker).width).toBe(`${theme.layout.categoryDot}px`);
    },
  );

  it('draws «Удалённые» as a ring in categoryColour(Removed), not a filled dot', () => {
    const marker = renderCell({ code: 'Removed', label: 'Удалённые' });
    const style = getComputedStyle(marker);

    expect(marker).toHaveAttribute('aria-hidden', 'true');
    expect(style.borderTopColor).toBe(rgb(theme.workTracking.categoryColour('Removed')));
    expect(style.borderTopWidth).toBe(theme.spacing(0.25));
    expect(style.backgroundColor).not.toBe(rgb(theme.workTracking.categoryColour('Removed')));
  });

  it('gives an unknown code a filled dot in the secondary text colour, and its label', () => {
    const marker = renderCell({
      code: 'Archived' as StateCategory['code'],
      label: 'Архив',
    });

    expect(getComputedStyle(marker).backgroundColor).toBe(rgb(theme.palette.text.secondary));
    expect(screen.getByRole('cell')).toHaveTextContent(/^Архив$/);
  });
});
