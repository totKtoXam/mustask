import { IconAction, type IconActionProps } from '../actions/IconAction';

export interface RowControlProps extends Omit<IconActionProps, 'size'> {
  /**
   * `tab` (default): in the Tab order. `roving`: `tabIndex` -1 unless the host passes one
   * — a treegrid host keeps the roving tabindex itself (the current row's control gets 0),
   * owns the arrow keys and the focus, and this part does nothing about focus.
   */
  focus?: 'tab' | 'roving';
}

/**
 * A row's own small control — «+», «⋮» (tech-015 design § 5.11): an `IconAction` of the
 * row size (24 px box, 16 px glyph) that the row reveals by its hover, by focus within
 * it, by its selection or while its own menu is open. Hidden, it is `opacity: 0`, not
 * `visibility` or `display`, so it stays focusable.
 *
 * A row's controls name their row — «Добавить: Фича (MSK-EPIC-1)», «Ещё действия:
 * MSK-EPIC-1» — and are `inactive` while a write is in flight.
 */
export function RowControl({ focus = 'tab', tabIndex, ...action }: RowControlProps) {
  return (
    <IconAction
      {...action}
      size="row"
      data-row-control=""
      tabIndex={focus === 'roving' ? (tabIndex ?? -1) : tabIndex}
    />
  );
}
