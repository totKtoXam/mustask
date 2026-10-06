// The browser's own drag and drop, as a test drives it. jsdom has neither
// `DataTransfer` nor `DragEvent`, so Testing Library builds a plain event and
// sets `dataTransfer` on it; `clientY` is set the same way. Every row's box is
// all zeros in jsdom, so `clientY` -1 is a row's upper half and 1 its lower.
import { createEvent, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';

export interface TestDataTransfer {
  effectAllowed: string;
  dropEffect: string;
  setData(format: string, value: string): void;
  getData(format: string): string;
  setDragImage: ReturnType<typeof vi.fn>;
}

/** One drag's `dataTransfer`, shared by every event of that drag. */
export function dataTransfer(): TestDataTransfer {
  const data: Record<string, string> = {};
  return {
    effectAllowed: 'all',
    dropEffect: 'none',
    setData: (format, value) => {
      data[format] = value;
    },
    getData: (format) => data[format] ?? '',
    setDragImage: vi.fn(),
  };
}

type DragEventName = 'dragStart' | 'dragEnter' | 'dragOver' | 'dragLeave' | 'drop' | 'dragEnd';

/** Fires one drag event on `element`; `half` sets `clientY` against a row's middle. */
export function fireDrag(
  name: DragEventName,
  element: Element,
  transfer: TestDataTransfer,
  half?: 'upper' | 'lower',
): boolean {
  const event = createEvent[name](element, { dataTransfer: transfer });
  if (half) {
    Object.defineProperty(event, 'clientY', { value: half === 'upper' ? -1 : 1 });
  }
  return fireEvent(element, event);
}
