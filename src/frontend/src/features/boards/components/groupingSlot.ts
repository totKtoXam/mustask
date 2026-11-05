import { createContext, useContext, type ReactNode } from 'react';

/**
 * The «Группировка» switch of a shipped page (feat-048 BR-AV-2, OQ-7/OQ-8 A): the page above puts
 * it here and `TeamHead` draws it beside the level drop-down, so the shipped pages are not
 * threaded with a prop each.
 */
export const GroupingSlot = createContext<ReactNode>(null);

export function useGroupingSlot(): ReactNode {
  return useContext(GroupingSlot);
}
