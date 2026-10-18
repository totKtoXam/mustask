import { describe, expect, it } from 'vitest';
import type { WorkItemListFilter } from '../api/workItemsApi';
import { process } from '../testFixtures';
import {
  createdInListNotice,
  filterContent,
  filterMismatch,
  filterOf,
  isFiltered,
  matchesKeyword,
  stateOptions,
  withFilter,
} from './workItemFilter';

// feat-003 BR-WS-2..-14, design §§ 2-6: the address form of the four filters, the options of
// «Статус», the keyword's meaning and the notice's judgement of a new item.

const NONE: WorkItemListFilter = { state: [], assigneeId: [], search: '', teamId: [] };

describe('feat-003 BR-WS-11 the filter an address carries', () => {
  it('reads each dimension in the address’s order, each value once', () => {
    expect(
      filterOf(
        '?state=%D0%9D%D0%BE%D0%B2%D0%B0%D1%8F&state=%D0%9D%D0%BE%D0%B2%D0%B0%D1%8F&assigneeId=u1&assigneeId=none&teamId=t1&search=%D0%BE%D0%BF%D0%BB%D0%B0%D1%82',
      ),
    ).toEqual({
      state: ['Новая'],
      assigneeId: ['u1', 'none'],
      search: 'оплат',
      teamId: ['t1'],
    });
  });

  it('is empty without the parameters; a blank value is left out and sets no filter', () => {
    expect(filterOf('')).toEqual(NONE);
    expect(filterOf('?workitem=MSK-US-2')).toEqual(NONE);
    expect(filterOf('?state=&state=%20&assigneeId=&search=%20%20&teamId=')).toEqual(NONE);
  });

  it('keeps a value as written whether it names anything or not (BR-WS-10)', () => {
    expect(filterOf('?state=готово&state=новая&assigneeId=NONE&assigneeId=not-a-user')).toEqual({
      ...NONE,
      state: ['готово', 'новая'],
      assigneeId: ['NONE', 'not-a-user'],
    });
  });

  it('takes the first non-blank keyword, trimmed (BR-WS-9)', () => {
    expect(filterOf('?search=&search=%20%D0%B0%D0%B1%20&search=second').search).toBe('аб');
  });
});

describe('feat-003 BR-WS-11 the address a filter makes', () => {
  it('writes state, assignee, keyword and team in that order, after every other parameter', () => {
    const next = withFilter('?workitem=MSK-US-2&teamId=old&sort=title', {
      state: ['Новая', 'Разрешена'],
      assigneeId: ['u1', 'none'],
      search: ' оплат ',
      teamId: ['t1'],
    });

    expect(next).toBe(
      `?workitem=MSK-US-2&sort=title&state=${encodeURIComponent('Новая')}&state=${encodeURIComponent('Разрешена')}` +
        `&assigneeId=u1&assigneeId=none&search=${encodeURIComponent('оплат')}&teamId=t1`,
    );
  });

  it('leaves blank values and repeats out, and nothing at all when nothing is set', () => {
    expect(withFilter('?state=a&assigneeId=b&search=c&teamId=d', NONE)).toBe('');
    expect(withFilter('?sort=title&state=a', NONE)).toBe('?sort=title');
    expect(
      withFilter('', {
        state: ['a', 'a', ' '],
        assigneeId: [''],
        search: '   ',
        teamId: ['', 't'],
      }),
    ).toBe('?state=a&teamId=t');
  });

  it('reads back what it wrote', () => {
    const filter: WorkItemListFilter = {
      state: ['Удалена', 'Новая'],
      assigneeId: ['none', 'u&1'],
      search: '100% ё',
      teamId: ['none'],
    };

    expect(filterOf(withFilter('', filter))).toEqual(filter);
  });
});

describe('feat-003 design § 5 filtered and the page’s key', () => {
  it('is filtered when any dimension holds a value, one that names nothing included; a blank keyword is none', () => {
    expect(isFiltered(NONE)).toBe(false);
    expect(isFiltered({ ...NONE, state: ['готово'] })).toBe(true);
    expect(isFiltered({ ...NONE, assigneeId: ['none'] })).toBe(true);
    expect(isFiltered({ ...NONE, search: 'х' })).toBe(true);
    expect(isFiltered({ ...NONE, teamId: ['t'] })).toBe(true);
  });

  it('compares by content: the order values were chosen in does not count, a value does', () => {
    const first = { ...NONE, state: ['b', 'a'], assigneeId: ['2', '1'] };
    const same = { ...NONE, state: ['a', 'b'], assigneeId: ['1', '2'] };

    expect(filterContent(first)).toBe(filterContent(same));
    expect(filterContent({ ...first, search: 'x' })).not.toBe(filterContent(first));
    expect(filterContent({ ...first, teamId: ['t'] })).not.toBe(filterContent(first));
  });
});

describe('feat-003 BR-WS-3 the options of «Статус»', () => {
  it('are the process’s state names, each once, in the order they first appear across its types', () => {
    expect(stateOptions(process).map((option) => option.label)).toEqual([
      'Новая',
      'Активна',
      'Разрешена',
      'Закрыта',
      'Удалена',
    ]);
    expect(stateOptions(process).every((option) => option.value === option.label)).toBe(true);
  });
});

const ITEM = {
  identifier: 'MSK-BUG-42',
  number: 42,
  title: 'Падение при входе: Ёлка',
};

describe('feat-003 BR-WS-9 the keyword', () => {
  it.each([
    ['a piece of the title', 'при входе'],
    ['the title in another letter case', 'ПАДЕНИЕ'],
    ['«ё» read as «е», both ways', 'елка'],
    ['«ё» in the keyword against «е» in the title', 'ёлка'],
    ['the identifier', 'MSK-BUG-42'],
    ['the identifier in another letter case', 'msk-bug-42'],
    ['the number, in digits', '42'],
    ['surrounded by white space', '  42  '],
    ['nothing', ''],
  ])('finds the item by %s', (_name, keyword) => {
    expect(matchesKeyword(ITEM, keyword)).toBe(true);
  });

  it.each([
    ['a part of the identifier', 'MSK-BUG-4'],
    ['a part of the identifier from the type', 'BUG-42'],
    ['another type’s identifier', 'MSK-US-42'],
    ['another project’s identifier', 'OTH-BUG-42'],
    ['the number with a leading zero', '042'],
    ['another number', '43'],
    ['an underscore, which is no wildcard', 'П_дение'],
    ['a percent sign standing for anything', '%'],
    ['a text with a line break', 'Падение\nпри входе'],
    ['a text with U+0000', `При${String.fromCharCode(0)}`],
    ['a text of 256 characters', 'я'.repeat(256)],
  ])('does not find the item by %s', (_name, keyword) => {
    expect(matchesKeyword(ITEM, keyword)).toBe(false);
  });

  it('treats «%» and «_» as themselves: a title holding one is found by it, no other is', () => {
    expect(matchesKeyword({ ...ITEM, title: 'Скидка 100%' }, '100%')).toBe(true);
    expect(matchesKeyword({ ...ITEM, title: 'Скидка 100%' }, '10_%')).toBe(false);
    expect(matchesKeyword({ ...ITEM, title: 'файл_1' }, '_')).toBe(true);
  });

  it('finds a title that contains a part of an identifier, and a number past int by the title alone', () => {
    expect(matchesKeyword({ ...ITEM, title: 'См. BUG-42' }, 'BUG-42')).toBe(true);
    expect(matchesKeyword({ ...ITEM, title: 'Счёт 99999999999' }, '99999999999')).toBe(true);
    expect(matchesKeyword(ITEM, '99999999999')).toBe(false);
  });
});

describe('feat-003 BR-WS-14 a new item against the filter', () => {
  const item = {
    ...ITEM,
    state: { id: 's', name: 'Новая', category: { code: 'Proposed', label: '' } },
    assignee: null,
    team: null,
  };

  it('fits when nothing is set, and when every dimension that is set takes it', () => {
    expect(filterMismatch(item, NONE)).toBe(false);
    expect(
      filterMismatch(
        { ...item, assignee: { id: 'u1', displayName: 'А', email: '', isMember: true } },
        { state: ['Активна', 'Новая'], assigneeId: ['u1'], search: 'msk-bug-42', teamId: [] },
      ),
    ).toBe(false);
    expect(
      filterMismatch(item, { ...NONE, assigneeId: ['none'], teamId: ['none'], search: 'ёлка' }),
    ).toBe(false);
  });

  it.each([
    ['the state', { state: ['Активна'] }],
    ['the assignee (an item with none, a filter on one person)', { assigneeId: ['u1'] }],
    ['the team (an item with none, a filter on one team)', { teamId: ['t1'] }],
    ['the keyword', { search: 'оплата' }],
  ])('does not fit when it fails %s', (_name, change) => {
    expect(filterMismatch(item, { ...NONE, ...change })).toBe(true);
  });

  it('judges a person against the ids and «Не назначен» against `none`', () => {
    const assigned = {
      ...item,
      assignee: { id: 'u2', displayName: 'Б', email: '', isMember: true },
    };

    expect(filterMismatch(assigned, { ...NONE, assigneeId: ['u1', 'u2'] })).toBe(false);
    expect(filterMismatch(assigned, { ...NONE, assigneeId: ['none'] })).toBe(true);
  });

  it('reads the notice of today, and adds the reason when the filter hides the item', () => {
    expect(createdInListNotice(item, NONE)).toBe('Рабочий элемент MSK-BUG-42 создан.');
    expect(createdInListNotice(item, { ...NONE, state: ['Активна'] })).toBe(
      'Рабочий элемент MSK-BUG-42 создан. Он не подходит под текущий фильтр.',
    );
  });
});
