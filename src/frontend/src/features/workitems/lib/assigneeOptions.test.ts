import { describe, expect, it } from 'vitest';
import { assigneeLabel } from './assignee';
import {
  assigneeOptions,
  isSearchable,
  optionKey,
  optionLabel,
  optionOf,
  sameOption,
  searchTextOf,
} from './assigneeOptions';

const ANNA = { id: 'anna', displayName: 'Анна Смирнова', email: 'anna@example.com' };

describe('assigneeLabel (BR-M3-22; api-contract § 1.1)', () => {
  it('reads «Не назначен», the display name, or «Неизвестный пользователь»', () => {
    expect(assigneeLabel(null)).toBe('Не назначен');
    expect(assigneeLabel(ANNA)).toBe('Анна Смирнова');
    expect(assigneeLabel({ id: 'ghost', displayName: null, email: null })).toBe(
      'Неизвестный пользователь',
    );
  });
});

describe('the search text (spec § Pickers)', () => {
  it('is trimmed, and searchable from 2 characters', () => {
    expect(searchTextOf('  ан  ')).toBe('ан');
    expect(isSearchable('а')).toBe(false);
    expect(isSearchable('ан')).toBe(true);
  });
});

describe('assigneeOptions: «Не назначен» first, then the search’s lines', () => {
  const labels = (search: Parameters<typeof assigneeOptions>[0]) =>
    assigneeOptions(search).map(optionLabel);

  it('below 2 characters', () => {
    expect(labels({ status: 'tooShort' })).toEqual([
      'Не назначен',
      'Введите не менее 2 символов имени или почты',
    ]);
  });

  it('while searching and when the search failed', () => {
    expect(labels({ status: 'searching' })).toEqual(['Не назначен', 'Поиск…']);
    expect(labels({ status: 'failed' })).toEqual([
      'Не назначен',
      'Не удалось выполнить поиск. Попробуйте ещё раз.',
    ]);
  });

  it('nobody matched', () => {
    expect(labels({ status: 'done', result: { users: [], moreMatched: false } })).toEqual([
      'Не назначен',
      'Никого не найдено',
    ]);
  });

  it('users as the server ordered them, and the last line when more than 20 matched', () => {
    const users = Array.from({ length: 20 }, (_, index) => ({
      id: `u${index}`,
      displayName: `Пользователь ${index}`,
      email: '',
    }));
    const lines = labels({ status: 'done', result: { users, moreMatched: true } });
    expect(lines).toHaveLength(22);
    expect(lines[1]).toBe('Пользователь 0');
    expect(lines.at(-1)).toBe('Показаны первые 20. Уточните запрос.');
    expect(labels({ status: 'done', result: { users, moreMatched: false } })).toHaveLength(21);
  });
});

describe('options and their identity', () => {
  it('a value is «Не назначен» or that user, keyed by id', () => {
    expect(optionOf(null)).toEqual({ kind: 'none' });
    expect(optionKey(optionOf(ANNA))).toBe('user:anna');
    expect(
      sameOption(optionOf(ANNA), { kind: 'user', user: { ...ANNA, displayName: 'А. С.' } }),
    ).toBe(true);
    expect(sameOption(optionOf(ANNA), optionOf(null))).toBe(false);
  });
});
