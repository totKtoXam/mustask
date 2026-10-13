import { describe, expect, it } from 'vitest';
import { descriptionError, titleError } from './rules';

// api-contract § 0.6, BR-M3-3, BR-M3-19: the browser's checks, in the server's order.

describe('titleError', () => {
  it.each([
    ['', 'Введите заголовок.'],
    ['   ', 'Введите заголовок.'],
    ['\t\n', 'Введите заголовок.'],
    ['я'.repeat(256), 'Не более 255 символов.'],
    [`  ${'я'.repeat(256)}  `, 'Не более 255 символов.'],
    ['Сборка\nпадает', 'Заголовок должен быть в одну строку.'],
    ['Сборка\rпадает', 'Заголовок должен быть в одну строку.'],
    ['Сборка\vпадает', 'Заголовок должен быть в одну строку.'],
    ['Сборка\fпадает', 'Заголовок должен быть в одну строку.'],
    [`Сборка${String.fromCharCode(0x85)}падает`, 'Заголовок должен быть в одну строку.'],
    [`Сборка${String.fromCharCode(0x2028)}падает`, 'Заголовок должен быть в одну строку.'],
    [`Сборка${String.fromCharCode(0x2029)}падает`, 'Заголовок должен быть в одну строку.'],
  ])('refuses %j with «%s»', (title, message) => {
    expect(titleError(title)).toBe(message);
  });

  it('checks the length before the line break, as the server does', () => {
    expect(titleError(`${'я'.repeat(256)}\nx`)).toBe('Не более 255 символов.');
  });

  it.each([
    ['Настроить сборку'],
    ['  Настроить сборку  '],
    ['я'.repeat(255)],
    [` ${'я'.repeat(255)}\n`],
    ['Сборка\n'],
  ])('accepts %j: trimmed it is one line of 1..255', (title) => {
    expect(titleError(title)).toBeNull();
  });
});

// feat-018 BR-RT-7, US-4: the draft's description is the editor's canonical html.
describe('descriptionError', () => {
  it('accepts 0..100 000 visible characters, markup included', () => {
    expect(descriptionError('')).toBeNull();
    expect(descriptionError('<p><strong>важно</strong></p><p>шаг 2</p>')).toBeNull();
    expect(descriptionError(`<p>${'о'.repeat(100_000)}</p>`)).toBeNull();
  });

  it('refuses 100 001 visible characters with «Не более 100 000 символов.»', () => {
    expect(descriptionError(`<p>${'о'.repeat(100_001)}</p>`)).toBe('Не более 100 000 символов.');
  });

  it('counts by code point, never by UTF-16 unit', () => {
    expect(descriptionError(`<p>${'😀'.repeat(100_000)}</p>`)).toBeNull();
    expect(descriptionError(`<p>${'😀'.repeat(100_001)}</p>`)).toBe('Не более 100 000 символов.');
  });

  it('refuses markup over the ceiling with its own text, which wins over the visible limit', () => {
    const text = 'Слишком много разметки. Упростите форматирование или сократите текст.';
    expect(descriptionError(`<p>${'<b>я</b>x'.repeat(60_000)}</p>`)).toBe(text);
  });
});
