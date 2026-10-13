import { createContext, useContext } from 'react';

/**
 * What a `TableCard` tells its parts: a `treegrid` gives the header row, the head cells
 * and the cells their explicit WAI-ARIA roles, as `TreeRegistry` has them today.
 */
export interface TableContextValue {
  role: 'table' | 'treegrid';
}

export const TableContext = createContext<TableContextValue>({ role: 'table' });

export function useTableRole(): TableContextValue['role'] {
  return useContext(TableContext).role;
}
