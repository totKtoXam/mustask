import { describe, expect, it } from 'vitest';
import type { AssigneeFilterOption } from '../api/workItemsApi';
import { assigneeChoices, tellApart } from './assigneeFilterOptions';

// feat-003 BR-WS-6, BR-WS-7, design § 2.3, D-2: the options of «Исполнитель» as the picker draws them.

function person(
  id: string,
  displayName: string | null,
  overrides: Partial<AssigneeFilterOption> = {},
): AssigneeFilterOption {
  return {
    id,
    displayName,
    email: displayName === null ? null : `${id}@alfa.test`,
    isActive: displayName !== null,
    isMember: true,
    ...overrides,
  };
}

describe('feat-003 BR-WS-7 how an option reads', () => {
  it('is the name, «(не в организации)» for a former member, «(неактивен)» for a disabled account', () => {
    const options = [
      person('mia', 'Мия Петрова'),
      person('ivan', 'Иван Котов', { isMember: false }),
      person('petr', 'Пётр Ли', { isActive: false }),
      person('both', 'Оба Знака', { isMember: false, isActive: false }),
      person('ghost', null),
      person('ghost-gone', null, { isMember: false }),
    ];

    expect(tellApart(options)).toEqual([
      'Мия Петрова',
      'Иван Котов (не в организации)',
      'Пётр Ли (неактивен)',
      'Оба Знака (не в организации)',
      'Неизвестный пользователь',
      'Неизвестный пользователь (не в организации)',
    ]);
  });
});

describe('feat-003 D-2 alike options', () => {
  it('tells equal names apart by e-mail, in the text itself, and only them', () => {
    const options = [
      person('a', 'Иван Котов', { email: 'ivan.k@alfa.test' }),
      person('b', 'Иван Котов', { email: 'i.kotov@beta.test' }),
      person('c', 'Мия Петрова'),
    ];

    expect(tellApart(options)).toEqual([
      'Иван Котов — ivan.k@alfa.test',
      'Иван Котов — i.kotov@beta.test',
      'Мия Петрова',
    ]);
  });

  it('keeps the marks, and puts the e-mail after the whole text', () => {
    const options = [
      person('a', 'Иван Котов', { isMember: false, email: 'a@x' }),
      person('b', 'Иван Котов', { isMember: false, email: 'b@x' }),
    ];

    expect(tellApart(options)).toEqual([
      'Иван Котов (не в организации) — a@x',
      'Иван Котов (не в организации) — b@x',
    ]);
  });

  it('numbers unknown users after the name and before the marks, by their place among the alike', () => {
    const options = [
      person('u1', null, { isMember: false }),
      person('u2', null, { isMember: false }),
      person('u3', null),
    ];

    expect(tellApart(options)).toEqual([
      'Неизвестный пользователь № 1 (не в организации)',
      'Неизвестный пользователь № 2 (не в организации)',
      'Неизвестный пользователь',
    ]);
  });

  it('numbers people whose e-mail does not tell them apart: none, empty, or equal ignoring case', () => {
    const options = [
      person('a', 'Анна Ким', { email: '' }),
      person('b', 'Анна Ким', { email: '' }),
      person('c', 'Лев Ким', { email: 'Lev@X.test' }),
      person('d', 'Лев Ким', { email: 'lev@x.test' }),
    ];

    expect(tellApart(options)).toEqual([
      'Анна Ким № 1',
      'Анна Ким № 2',
      'Лев Ким № 1',
      'Лев Ким № 2',
    ]);
  });

  it('mixes the two in one group: the e-mail for the one who differs, the number for the rest', () => {
    const options = [
      person('a', 'Иван Котов', { email: 'same@x' }),
      person('b', 'Иван Котов', { email: 'other@x' }),
      person('c', 'Иван Котов', { email: 'same@x' }),
      person('d', null),
    ];

    expect(tellApart(options)).toEqual([
      'Иван Котов № 1',
      'Иван Котов — other@x',
      'Иван Котов № 3',
      'Неизвестный пользователь',
    ]);
  });

  it('gives nothing to a pair that already reads differently', () => {
    const options = [
      person('a', 'Иван Котов', { email: 'a@x' }),
      person('b', 'Иван Котов', { email: 'b@x', isActive: false }),
    ];

    expect(tellApart(options)).toEqual(['Иван Котов', 'Иван Котов (неактивен)']);
  });
});

describe('feat-003 BR-WS-6 the picker’s choices', () => {
  it('are the people in the read’s order by user id, then «Не назначен» by `none`', () => {
    const choices = assigneeChoices([person('mia', 'Мия Петрова'), person('ann', 'Анна Смирнова')]);

    expect(choices).toEqual([
      { value: 'mia', label: 'Мия Петрова' },
      { value: 'ann', label: 'Анна Смирнова' },
      { value: 'none', label: 'Не назначен' },
    ]);
  });

  it('are «Не назначен» alone when nobody is assigned', () => {
    expect(assigneeChoices([])).toEqual([{ value: 'none', label: 'Не назначен' }]);
  });
});
