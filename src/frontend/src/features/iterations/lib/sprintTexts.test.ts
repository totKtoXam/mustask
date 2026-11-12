import { describe, expect, it } from 'vitest';
import { itemsCount, plural, sprintsCount } from './sprintTexts';

describe('mod-007: Russian plurals', () => {
  it.each([
    [1, '1 элемент'],
    [2, '2 элемента'],
    [5, '5 элементов'],
    [11, '11 элементов'],
    [21, '21 элемент'],
    [112, '112 элементов'],
  ])('counts %s items as «%s»', (n, text) => {
    expect(itemsCount(n)).toBe(text);
  });

  it('counts sprints', () => {
    expect(sprintsCount(3)).toBe('3 спринта');
    expect(plural(0, 'a', 'b', 'c')).toBe('c');
  });
});
