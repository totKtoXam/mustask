// The aggregate board's columns are the server's: every group of the board's groups read carries
// the level's columns with their header, category and drop targets (api-contract § 2.1), the
// project board's own column fields, so nothing is derived from the process here.
import type { GroupHeader } from '../api/aggregateApi';

export interface AggregateColumn {
  /** `<Category>-<index>`, the key of the groups and cells reads. */
  key: string;
  header: string;
  /** The state a card of the type takes when dropped here; absent: no drop target for it. */
  stateOfType: ReadonlyMap<string, string>;
}

/** The columns of the board, from the first group (every group lists the same ones). */
export function columnsOf(groups: readonly GroupHeader[]): AggregateColumn[] {
  return (groups[0]?.columns ?? []).map((column) => ({
    key: column.key,
    header: column.header,
    stateOfType: new Map(
      column.targets.flatMap((target) =>
        target.stateId === null ? [] : [[target.typeId, target.stateId] as const],
      ),
    ),
  }));
}
