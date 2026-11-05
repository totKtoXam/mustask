// The pointer drag of a row inside one group of a by-project backlog (feat-048 design § 5, § 6,
// OQ-6 A): the browser's own drag and drop, the drop line standing between the loaded rows only.
// A group holds the rows of one project, so a drop in another group is no target (the drag state
// is the group's own). Below the last loaded row is a target only when no `nextCursor` remains.
import { useState, type DragEvent } from 'react';
import { isEffectiveGap } from '../lib/reorder';

/** The format a drag carries, so a drag from elsewhere on the page is not mistaken for a row. */
const ROW_FORMAT = 'application/x-mustask-aggregate-row';

export interface RowDragState {
  from: number;
  /** The drop line's gap, or `null` off the rows. */
  gap: number | null;
}

export interface RowDragHandlers {
  draggable: true;
  onDragStart(event: DragEvent<HTMLElement>): void;
  onDragEnd(): void;
  onDragOver(event: DragEvent<HTMLElement>): void;
  onDragLeave(): void;
  onDrop(event: DragEvent<HTMLElement>): void;
}

/** Where a pointer at `clientY` over a row's box puts the line: above its middle, or below it. */
function gapOf(event: DragEvent<HTMLElement>, index: number): number {
  const box = event.currentTarget.getBoundingClientRect();
  return event.clientY < box.top + box.height / 2 ? index : index + 1;
}

export function useRowDrag({
  count,
  partial,
  onDrop,
}: {
  count: number;
  /** More rows remain unloaded: the end of the loaded rows is not a target. */
  partial: boolean;
  onDrop(from: number, gap: number): void;
}) {
  const [drag, setDrag] = useState<RowDragState | null>(null);

  const targetGap = (gap: number, from: number): number | null =>
    isEffectiveGap(count, from, gap) && !(partial && gap === count) ? gap : null;

  const handlers = (index: number): RowDragHandlers => ({
    draggable: true,
    onDragStart: (event) => {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData(ROW_FORMAT, String(index));
      setDrag({ from: index, gap: null });
    },
    onDragEnd: () => setDrag(null),
    onDragOver: (event) => {
      if (!drag) return;
      const gap = targetGap(gapOf(event, index), drag.from);
      if (gap !== null) {
        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
      } else {
        event.dataTransfer.dropEffect = 'none';
      }
      if (gap !== drag.gap) setDrag({ from: drag.from, gap });
    },
    onDragLeave: () => {
      if (drag && drag.gap !== null) setDrag({ from: drag.from, gap: null });
    },
    onDrop: (event) => {
      if (!drag) return;
      const gap = targetGap(gapOf(event, index), drag.from);
      event.preventDefault();
      setDrag(null);
      if (gap !== null) onDrop(drag.from, gap);
    },
  });

  return { drag, handlers };
}
