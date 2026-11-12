import { useCallback, useMemo, useState, type KeyboardEvent, type RefObject } from 'react';

interface Roving {
  /** The one control with `tabIndex` 0. */
  tabStop: string | null;
  /** Put on the toolbar element. */
  onKeyDown: (event: KeyboardEvent<HTMLElement>) => void;
  /** Put on each control, with its id: remembers where focus was. */
  onFocusControl: (id: string) => void;
}

/**
 * A roving tabindex for the toolbar (design § Keyboard; MUI has no primitive
 * for it, so this is the small hook the design names): one tab stop, ←/→ move
 * over the enabled controls with wrap, Home/End to the first and last, Enter
 * and Space activate (the buttons are native), Tab is not intercepted. It
 * remembers the last control, and when that one is disabled the stop moves to
 * the nearest enabled one.
 *
 * Events whose target is outside the toolbar's own DOM are ignored: menus and
 * the dialog render in portals but React's synthetic events bubble through the
 * React tree, so an Esc or an arrow from a menu must not be taken here.
 */
export function useRovingToolbar(
  containerRef: RefObject<HTMLElement | null>,
  order: readonly string[],
  enabled: ReadonlySet<string>,
): Roving {
  const [remembered, setRemembered] = useState<string | null>(null);

  const enabledOrder = useMemo(() => order.filter((id) => enabled.has(id)), [order, enabled]);
  let tabStop: string | null = null;
  if (remembered !== null && enabled.has(remembered)) {
    tabStop = remembered;
  } else if (remembered !== null) {
    // The nearest enabled one: forward from the remembered control, else backward.
    const at = order.indexOf(remembered);
    tabStop =
      order.slice(at + 1).find((id) => enabled.has(id)) ??
      order
        .slice(0, Math.max(at, 0))
        .reverse()
        .find((id) => enabled.has(id)) ??
      null;
  } else {
    tabStop = enabledOrder[0] ?? null;
  }

  const onKeyDown = useCallback(
    (event: KeyboardEvent<HTMLElement>) => {
      const container = containerRef.current;
      const target = event.target as HTMLElement;
      if (!container || !container.contains(target)) return;
      if (event.ctrlKey || event.metaKey || event.altKey) return;

      const current = target.closest<HTMLElement>('[data-control-id]')?.dataset.controlId;
      if (current === undefined) return;

      const position = enabledOrder.indexOf(current);
      let next: number;
      switch (event.key) {
        case 'ArrowRight':
          next = position < 0 ? 0 : (position + 1) % enabledOrder.length;
          break;
        case 'ArrowLeft':
          next =
            position < 0
              ? enabledOrder.length - 1
              : (position - 1 + enabledOrder.length) % enabledOrder.length;
          break;
        case 'Home':
          next = 0;
          break;
        case 'End':
          next = enabledOrder.length - 1;
          break;
        default:
          return;
      }

      const id = enabledOrder[next];
      if (id === undefined) return;
      event.preventDefault();
      setRemembered(id);
      container.querySelector<HTMLElement>(`[data-control-id="${id}"]`)?.focus();
    },
    [containerRef, enabledOrder],
  );

  return { tabStop, onKeyDown, onFocusControl: setRemembered };
}
