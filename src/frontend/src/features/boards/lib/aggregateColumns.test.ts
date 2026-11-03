import { describe, expect, it } from 'vitest';
import type { GroupHeader } from '../api/aggregateApi';
import { columnsOf } from './aggregateColumns';

const group = (columns: GroupHeader['columns']): GroupHeader => ({
  kind: 'project',
  id: 'p',
  name: 'P',
  count: 1,
  total: 1,
  columns,
});

describe('feat-048: the board columns come from the groups read', () => {
  it('maps the server header and the non-null targets of each column', () => {
    const columns = columnsOf([
      group([
        {
          key: 'Proposed-1',
          header: 'Новая',
          category: { code: 'Proposed', label: 'Предложено' },
          targets: [
            { typeId: 't-story', stateId: 's-new' },
            { typeId: 't-bug', stateId: null },
          ],
          count: 3,
        },
      ]),
    ]);
    expect(columns).toHaveLength(1);
    expect(columns[0]?.key).toBe('Proposed-1');
    expect(columns[0]?.header).toBe('Новая');
    expect([...(columns[0]?.stateOfType ?? [])]).toEqual([['t-story', 's-new']]);
  });

  it('has no columns for a read without groups or without columns', () => {
    expect(columnsOf([])).toEqual([]);
    expect(columnsOf([group(undefined)])).toEqual([]);
  });
});
