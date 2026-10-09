import { describe, expect, it } from 'vitest';
import {
  INCOMPLETE_DATE,
  dateError,
  formatNumber,
  numberError,
  parseNumber,
  textError,
} from './fieldValues';

// feat-022 BR-M3-32, BR-M3-35, BR-M3-36; spec § Controls and copy.

describe('formatNumber (BR-M3-32)', () => {
  it.each([
    [2.5, '2,5'],
    [8, '8'],
    [0, '0'],
    [13.25, '13,25'],
    [9999, '9999'],
    [null, ''],
  ])('%j is shown as «%s»', (value, text) => {
    expect(formatNumber(value)).toBe(text);
  });
});

describe('parseNumber', () => {
  it.each([
    ['5', 5, 0],
    ['5,5', 5.5, 1],
    ['2.5', 2.5, 1],
    ['2,50', 2.5, 1],
    ['  13,5  ', 13.5, 1],
    [',5', 0.5, 1],
    ['5,', 5, 0],
    ['-1', -1, 0],
    ['2,555', 2.555, 3],
    ['0', 0, 0],
  ])('reads «%s» as %d with %i significant decimals', (text, value, decimals) => {
    expect(parseNumber(text)).toEqual({ kind: 'number', value, decimals });
  });

  it.each([[''], ['   ']])('«%s» is empty, never 0', (text) => {
    expect(parseNumber(text)).toEqual({ kind: 'empty' });
  });

  it.each([['пять'], ['1,2,3'], ['1.2.3'], ['1 000'], ['1e3'], ['+5'], ['5 ч'], [',']])(
    '«%s» is not a number',
    (text) => {
      expect(parseNumber(text)).toEqual({ kind: 'invalid' });
    },
  );
});

describe('numberError: in the server’s order — a number, its range, its precision', () => {
  it.each([
    ['пять', false, 'Введите число.'],
    ['-1', false, 'Допустимо значение от 0 до 9 999.'],
    ['10000', false, 'Допустимо значение от 0 до 9 999.'],
    ['10000,555', false, 'Допустимо значение от 0 до 9 999.'],
    ['2,555', false, 'Не более двух знаков после запятой.'],
    ['2,5', true, 'Введите целое число.'],
    ['-0,5', true, 'Допустимо значение от 0 до 9 999.'],
  ])('«%s» (integer: %s) is refused with «%s»', (text, integer, message) => {
    expect(numberError(text, integer)).toBe(message);
  });

  it.each([
    ['', false],
    ['0', false],
    ['9999', false],
    ['9999', true],
    ['2,55', false],
    ['2,500', false],
    ['40', true],
    ['40,0', true],
  ])('accepts «%s» (integer: %s)', (text, integer) => {
    expect(numberError(text, integer)).toBeNull();
  });
});

describe('dateError (BR-M3-35)', () => {
  it.each([
    [INCOMPLETE_DATE, 'Введите дату полностью.'],
    ['1999-12-31', 'Укажите дату с 2000 по 2099 год.'],
    ['2100-01-01', 'Укажите дату с 2000 по 2099 год.'],
    ['275760-01-01', 'Укажите дату с 2000 по 2099 год.'],
  ])('«%s» is refused with «%s»', (value, message) => {
    expect(dateError(value)).toBe(message);
  });

  it.each([[''], ['2000-01-01'], ['2099-12-31'], ['2026-11-01']])('accepts «%s»', (value) => {
    expect(dateError(value)).toBeNull();
  });
});

describe('textError (BR-M3-36)', () => {
  it('takes 0..100 000 characters, markup and line breaks included, and refuses 100 001', () => {
    expect(textError('')).toBeNull();
    expect(textError('<b>важно</b>\nшаг 2')).toBeNull();
    expect(textError('о'.repeat(100_000))).toBeNull();
    expect(textError('о'.repeat(100_001))).toBe('Не более 100 000 символов.');
  });
});
