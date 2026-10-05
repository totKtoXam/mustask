import { describe, expect, it } from 'vitest';
import { initials } from './initials';

describe('US-2 Scenario Outline: Initials', () => {
  it.each([
    ['Иван Петров', 'ИП'],
    ['Иван Петрович Сидоров', 'ИП'],
    ['ivan.petrov', 'I'],
    ['e2e-fixture', 'E'],
    ['  иван   петров  ', 'ИП'],
  ])('«%s» → «%s»', (name, expected) => {
    expect(initials(name)).toBe(expected);
  });

  it('gives no initials for an empty name, so the avatar shows a person icon', () => {
    expect(initials('')).toBeNull();
    expect(initials('   ')).toBeNull();
  });
});
