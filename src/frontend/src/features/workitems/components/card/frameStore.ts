import type { CardFrameProps } from './cardFrame';

/**
 * Where a card hands its frame to the dialog that holds it (feat-029 design § 6): the
 * dialog's one shell is drawn from the first frame of the click, before the card exists,
 * and stays the same element through the loading, the failed read, the not-found frame
 * and the card — so the card publishes what it draws into that shell instead of drawing
 * a shell of its own. A store, not state, so a card that publishes does not redraw the
 * host that holds it, and the shell draws only when a frame arrives.
 */
export interface FrameStore {
  get: () => CardFrameProps | null;
  set: (frame: CardFrameProps | null) => void;
  subscribe: (listener: () => void) => () => void;
}

export function createFrameStore(): FrameStore {
  let current: CardFrameProps | null = null;
  const listeners = new Set<() => void>();
  return {
    get: () => current,
    set: (frame) => {
      if (frame !== current) {
        current = frame;
        listeners.forEach((listener) => listener());
      }
    },
    subscribe: (listener) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
  };
}
