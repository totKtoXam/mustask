import { describe, expect, it } from 'vitest';
import { outcomeSearch, parseOutcomeQuery } from './outcomeQuery';

const parse = (search: string) => parseOutcomeQuery(new URLSearchParams(search));

describe('mod-007 design § 6.2: the outcome address', () => {
  it('defaults to the level «Истории» with no range', () => {
    expect(parse('')).toEqual({ level: 'stories' });
  });

  it('reads a level and a whole range', () => {
    expect(parse('level=epics&from=2026-01-01&to=2026-03-31')).toEqual({
      level: 'epics',
      from: '2026-01-01',
      to: '2026-03-31',
    });
  });

  it.each([
    ['an unknown level', 'level=tasks', { level: 'stories' }],
    ['half a range', 'from=2026-01-01', { level: 'stories' }],
    ['an unparsable date', 'from=2026-13-45&to=2026-14-01', { level: 'stories' }],
    ['a reversed range', 'from=2026-03-01&to=2026-01-01', { level: 'stories' }],
  ])('falls back on %s', (_name, search, query) => {
    expect(parse(search)).toEqual(query);
  });

  it('writes nothing for the defaults, and what differs otherwise', () => {
    expect(outcomeSearch({ level: 'stories' })).toBe('');
    expect(outcomeSearch({ level: 'features', from: '2026-01-01', to: '2026-02-01' })).toBe(
      '?level=features&from=2026-01-01&to=2026-02-01',
    );
  });
});
