// The aggregate board's columns. The grouped count read gives only `{ key, count }` per column
// (api-contract § 2.1), so the headers and what a drop writes are derived from the organization's
// process with the server's own rule (BoardLayout.Derive, BR-M4-2; bug-004 D-3), and the level
// is resolved as BoardLevel.Resolve does. Recorded in plan-frontend.md as a duplicated rule.
import type { ProcessDefinitionResponse, WorkItemType } from '../../process';
import type { BoardLevelCode } from './levels';

const CATEGORY_ORDER = ['Proposed', 'InProgress', 'Resolved', 'Completed'] as const;

export interface AggregateColumn {
  /** `<Category>-<index>`, the key of the groups and cells reads. */
  key: string;
  header: string;
  category: string;
  /** The state a card of the type takes when dropped here; absent: no drop target for it. */
  stateOfType: ReadonlyMap<string, string>;
}

/** The types of the board level, in the process's display order. */
export function levelTypes(
  process: ProcessDefinitionResponse,
  level: BoardLevelCode,
): readonly WorkItemType[] {
  const levels = process.backlogLevels;
  const portfolio = levels.filter((l) => l.kind === 'portfolio').sort((a, b) => a.rank - b.rank);
  const requirement = levels
    .filter((l) => l.kind === 'requirement')
    .sort((a, b) => b.rank - a.rank);
  const resolved =
    level === 'stories' ? requirement[0] : level === 'features' ? portfolio[0] : portfolio[1];
  return resolved ? process.workItemTypes.filter((t) => t.backlogLevel.id === resolved.id) : [];
}

export function aggregateColumns(
  process: ProcessDefinitionResponse,
  level: BoardLevelCode,
): AggregateColumn[] {
  const types = levelTypes(process, level);
  const columns: AggregateColumn[] = [];
  for (const category of CATEGORY_ORDER) {
    const perType = types.map((type) => ({
      id: type.id,
      states: type.states
        .filter((s) => s.category.code === category)
        .sort((a, b) => a.order - b.order),
    }));
    const count = perType.reduce((max, t) => Math.max(max, t.states.length), 0);
    for (let k = 0; k < count; k += 1) {
      const stateOfType = new Map<string, string>();
      for (const t of perType) {
        const state = t.states[k];
        if (state) stateOfType.set(t.id, state.id);
      }
      const header = perType.find((t) => t.states[k] !== undefined)?.states[k]?.name ?? '';
      columns.push({ key: `${category}-${k + 1}`, header, category, stateOfType });
    }
  }
  return columns;
}
