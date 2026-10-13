import { createRef } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { px, rgb, rulesFor, theme } from '../../../test/kitTesting';
import { BodyRow, type BodyRowProps } from './BodyRow';
import { Cell } from './Cell';
import { TableCard } from './TableCard';

// tech-015 design § 5.11 «BodyRow»: 36 px, white, no lines, a light hover, a framed selection,
// the drag source's dimming and a drop target's cues — and **no role, tabIndex or aria-* of
// its own**: whatever a treegrid host passes reaches the `tr`.

function renderRow(props: BodyRowProps = {}) {
  return render(
    <TableCard aria-label="Таблица" head={null}>
      <BodyRow data-testid="row" {...props}>
        <Cell>Первая</Cell>
        <Cell>Вторая</Cell>
        <Cell>Третья</Cell>
      </BodyRow>
    </TableCard>,
  );
}

function row() {
  return screen.getByTestId('row');
}

/** The declarations of the stylesheet's rule for the row whose selector holds `fragment`. */
function rule(fragment: string, media = '') {
  return rulesFor(row(), fragment).find((candidate) => candidate.media === media)?.declarations;
}

/** The fill of the kit's own hover rule — not MUI's, which a `hover` or a MUI-selected row asks for. */
function hoverFill() {
  return rulesFor(row(), ':hover').find(
    (candidate) =>
      !candidate.selector.includes('.Mui') && candidate.declarations['background-color'],
  )?.declarations['background-color'];
}

describe('tech-015 Scenario: BodyRow (design § 5.11)', () => {
  describe('Scenario: the row’s look', () => {
    it('is layout density.registryRowHeight high, on white, with no line under it', () => {
      renderRow();
      const style = getComputedStyle(row());
      expect(px(style.height)).toBe(theme.density.registryRowHeight);
      expect(style.backgroundColor).toBe(rgb(theme.palette.background.paper));
      for (const cell of Array.from(row().children)) {
        expect(getComputedStyle(cell).borderBottomStyle).not.toBe('solid');
      }
    });

    it('lightens on hover to the application background, and keeps the selected tint when selected', () => {
      renderRow();
      expect(hoverFill()).toBe(theme.palette.background.default);
    });

    it('keeps the selected row’s tint on hover', () => {
      renderRow({ selected: true });
      expect(hoverFill()).toBe(theme.palette.shell.rowSelected);
    });
  });

  describe('Scenario: the framed selection', () => {
    it('is rowSelected with data-selected, and never aria-selected: a plain row is not selectable that way', () => {
      renderRow({ selected: true });
      expect(row()).toHaveAttribute('data-selected', 'true');
      expect(getComputedStyle(row()).backgroundColor).toBe(rgb(theme.palette.shell.rowSelected));
      expect(row()).not.toHaveAttribute('aria-selected');
    });

    it('draws the accent frame as the cells’ inset shadows: 2 px at the first cell’s left, 1 px at the top and bottom, 1 px at the last cell’s right', () => {
      renderRow({ selected: true });
      const accent = theme.palette.primary.main;
      const { thick, thin } = theme.layout.frame;
      const edges = `inset 0 ${thin}px 0 ${accent}, inset 0 -${thin}px 0 ${accent}`;
      const compact = (value: string | undefined) => value?.replace(/,\s+/g, ',');
      expect(compact(rule('>td')?.['box-shadow'])).toBe(compact(edges));
      expect(compact(rule('>td:first-of-type')?.['box-shadow'])).toBe(
        compact(`inset ${thick}px 0 0 ${accent}, ${edges}`),
      );
      expect(compact(rule('>td:last-of-type')?.['box-shadow'])).toBe(
        compact(`inset -${thin}px 0 0 ${accent}, ${edges}`),
      );
      expect(thick).toBe(2);
      expect(thin).toBe(1);
    });

    it('draws no frame when it is not selected', () => {
      renderRow();
      expect(row()).not.toHaveAttribute('data-selected');
      expect(
        rulesFor(row(), '>td:first-of-type').filter(
          (candidate) => candidate.declarations['box-shadow'],
        ),
      ).toHaveLength(0);
    });

    it('calls onSelect on a click, after the host’s own onClick', async () => {
      const user = userEvent.setup();
      const calls: string[] = [];
      renderRow({ onClick: () => calls.push('click'), onSelect: () => calls.push('select') });
      await user.click(screen.getByText('Вторая'));
      expect(calls).toEqual(['click', 'select']);
    });

    it('is not a click target without onSelect', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      renderRow({ onClick });
      await user.click(screen.getByText('Первая'));
      expect(onClick).toHaveBeenCalledOnce();
    });
  });

  describe('Scenario: the drag source and the drop target', () => {
    it('dims the content of the row being dragged', () => {
      renderRow({ dragging: true });
      expect(row()).toHaveAttribute('data-dragging', 'true');
      expect(rule('[data-dragging="true"]>td')?.opacity).toBe(
        String(theme.layout.table.draggedOpacity),
      );
      expect(theme.layout.table.draggedOpacity).toBe(0.6);
    });

    it('frames an allowed target in a 2 px accent line inside the row, on the selection tint', () => {
      renderRow({ cue: 'dropAllowed' });
      expect(row()).toHaveAttribute('data-cue', 'dropAllowed');
      const declared = rule('[data-cue="dropAllowed"]');
      expect(declared?.outline).toBe(
        `${theme.layout.frame.thick}px solid ${theme.palette.primary.main}`,
      );
      expect(declared?.['outline-offset']).toBe(`-${theme.layout.frame.thick}px`);
      expect(declared?.['background-color']).toBe(theme.palette.action.selected);
    });

    it('frames a refused target in a dashed error line', () => {
      renderRow({ cue: 'dropRefused' });
      const declared = rule('[data-cue="dropRefused"]');
      expect(declared?.outline).toBe(
        `${theme.layout.frame.thick}px dashed ${theme.palette.error.main}`,
      );
    });

    it.each([
      ['dropLineTop', `inset 0 ${theme.layout.frame.thick}px 0 0 ${theme.palette.primary.main}`],
      [
        'dropLineBottom',
        `inset 0 -${theme.layout.frame.thick}px 0 0 ${theme.palette.primary.main}`,
      ],
    ] as const)('draws the %s as a 2 px accent line on the cells', (cue, shadow) => {
      renderRow({ cue });
      expect(rule(`[data-cue="${cue}"]>td`)?.['box-shadow']).toBe(shadow);
    });

    it('is drawn with no cue by default', () => {
      renderRow();
      expect(row()).not.toHaveAttribute('data-cue');
      expect(row()).not.toHaveAttribute('data-dragging');
    });
  });

  describe('Scenario: the row’s own controls are revealed by the row', () => {
    it('hides them at rest with opacity 0, not visibility or display, so they stay focusable', () => {
      renderRow();
      const declared = rule('[data-row-control]');
      expect(declared?.opacity).toBe('0');
      expect(declared?.visibility).toBeUndefined();
      expect(declared?.display).toBeUndefined();
    });

    it('shows them on the row’s hover where the pointer can hover', () => {
      renderRow();
      expect(rule(':hover [data-row-control]', '(hover: hover)')?.opacity).toBe('1');
      // Never on a bare `:hover`: on a touch screen it is sticky.
      expect(rule(':hover [data-row-control]')).toBeUndefined();
    });

    it('shows them on focus within the row, on selection and while their own menu is open', () => {
      renderRow();
      expect(rule(':focus-within [data-row-control]')?.opacity).toBe('1');
      expect(rule('[data-selected="true"] [data-row-control]')?.opacity).toBe('1');
      expect(rule('[data-row-control][aria-expanded="true"]')?.opacity).toBe('1');
    });
  });

  describe('Scenario: it sets nothing for the host to unset', () => {
    it('has no role, no tabIndex and no aria-* of its own — selected or not', () => {
      renderRow({ selected: true });
      for (const attribute of [
        'role',
        'tabindex',
        'aria-selected',
        'aria-level',
        'aria-expanded',
        'aria-setsize',
        'aria-posinset',
      ]) {
        expect(row()).not.toHaveAttribute(attribute);
      }
    });

    it('passes a treegrid host’s role, tabIndex and aria-* through to the tr as given', () => {
      renderRow({
        role: 'row',
        tabIndex: 0,
        'aria-level': 2,
        'aria-setsize': 5,
        'aria-posinset': 3,
        'aria-expanded': true,
        'aria-selected': false,
        id: 'row-3',
        'data-identifier': 'MSK-3',
      });
      expect(row()).toHaveAttribute('role', 'row');
      expect(row()).toHaveAttribute('tabindex', '0');
      expect(row()).toHaveAttribute('aria-level', '2');
      expect(row()).toHaveAttribute('aria-setsize', '5');
      expect(row()).toHaveAttribute('aria-posinset', '3');
      expect(row()).toHaveAttribute('aria-expanded', 'true');
      expect(row()).toHaveAttribute('aria-selected', 'false');
      expect(row()).toHaveAttribute('id', 'row-3');
      expect(row()).toHaveAttribute('data-identifier', 'MSK-3');
    });

    it('passes the handlers — keys, focus and drag — through', () => {
      const onKeyDown = vi.fn();
      const onFocus = vi.fn();
      const onDragStart = vi.fn();
      renderRow({ tabIndex: 0, draggable: true, onKeyDown, onFocus, onDragStart });
      expect(row()).toHaveAttribute('draggable', 'true');
      fireEvent.focus(row());
      fireEvent.keyDown(row(), { key: 'ArrowDown' });
      fireEvent.dragStart(row());
      expect(onFocus).toHaveBeenCalledOnce();
      expect(onKeyDown).toHaveBeenCalledOnce();
      expect(onDragStart).toHaveBeenCalledOnce();
    });

    it('hands its ref to the tr', () => {
      const ref = createRef<HTMLTableRowElement>();
      renderRow({ ref });
      expect(ref.current).toBe(row());
    });
  });
});
