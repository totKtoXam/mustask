import { describe, expect, it } from 'vitest';
import {
  KEY_FORMAT_TEXT,
  KEY_REQUIRED_TEXT,
  NAME_LENGTH_TEXT,
  descriptionError,
  keyError,
  nameError,
} from './validation';

describe('mod-002 US-1 Scenario: The name is trimmed and its length checked', () => {
  it.each([[''], ['   '], ['я'.repeat(101)], [`  ${'я'.repeat(101)}  `]])(
    'refuses %j with «Название должно содержать от 1 до 100 символов»',
    (value) => {
      expect(nameError(value)).toBe('Название должно содержать от 1 до 100 символов');
      expect(NAME_LENGTH_TEXT).toBe('Название должно содержать от 1 до 100 символов');
    },
  );

  it.each([['А'], ['Пример организации'], ['  Пример организации  '], ['я'.repeat(100)]])(
    'accepts %j, counted after trimming',
    (value) => {
      expect(nameError(value)).toBeNull();
    },
  );
});

describe('feat-028 BR-OL-3 Scenario: A description is at most 2 000 characters, as typed', () => {
  it.each([[''], ['Платформа и инструменты'], [' \n '], ['я'.repeat(2000)]])(
    'accepts %j — never trimmed, an empty one being no description',
    (value) => {
      expect(descriptionError(value)).toBeNull();
    },
  );

  it.each([['я'.repeat(2001)], [` ${'я'.repeat(2000)}`]])(
    'refuses a text of %j characters with «Описание должно содержать не более 2000 символов»',
    (value) => {
      expect(descriptionError(value)).toBe('Описание должно содержать не более 2000 символов');
    },
  );
});

describe('mod-002 US-3 Scenario: The key is required and well-formed', () => {
  it('refuses an empty key with «Укажите ключ проекта»', () => {
    expect(keyError('')).toBe('Укажите ключ проекта');
    expect(KEY_REQUIRED_TEXT).toBe('Укажите ключ проекта');
  });

  it.each([['prm'], ['1PRM'], ['P'], ['PROJECTKEY01'], ['РМК'], [' PRM'], ['PRM ']])(
    'refuses %j as typed, never trimmed or upper-cased',
    (value) => {
      expect(keyError(value)).toBe(
        'Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы',
      );
      expect(KEY_FORMAT_TEXT).toBe(keyError(value));
    },
  );

  it.each([['PRM'], ['P1'], ['ABCDEFGHIJ'], ['A123456789']])('accepts %j', (value) => {
    expect(keyError(value)).toBeNull();
  });
});
