import { describe, expect, it } from 'vitest';
import { DEFAULT_COPY } from '../copy';
import { TOOLBAR_GROUPS } from './model';
import { formatShortcut } from './formatShortcut';

/**
 * Adding a control is a deliberate edit of this test (plan-frontend § 6.3):
 * the model is data, and tasks B and C append to it. The marks menus, links,
 * every control of the design's 26 is here.
 */
describe('toolbar model', () => {
  it('lists the groups and controls in order', () => {
    expect(TOOLBAR_GROUPS.map((group) => [group.id, group.controls.map((c) => c.id)])).toEqual([
      ['history', ['undo', 'redo']],
      ['block', ['h3', 'h4', 'size']],
      ['marks', ['bold', 'italic', 'underline', 'strike', 'code', 'colour', 'highlight', 'clear']],
      ['lists', ['bullet', 'ordered', 'indent', 'outdent']],
      ['links', ['link', 'unlink', 'open', 'emoji']],
      ['code', ['codeblock', 'language', 'copy']],
      ['table', ['table', 'tableOps']],
    ]);
  });

  it('names every control in Russian, as the stories do, and has no duplicates', () => {
    const names = TOOLBAR_GROUPS.flatMap((group) =>
      group.controls.map((control) => control.name(DEFAULT_COPY)),
    );

    expect(names).toEqual([
      'Отменить',
      'Повторить',
      'Заголовок 3',
      'Заголовок 4',
      'Размер шрифта',
      'Полужирный',
      'Курсив',
      'Подчёркнутый',
      'Зачёркнутый',
      'Код в строке',
      'Цвет текста',
      'Выделение цветом',
      'Очистить форматирование',
      'Маркированный список',
      'Нумерованный список',
      'Увеличить отступ списка',
      'Уменьшить отступ списка',
      'Ссылка',
      'Убрать ссылку',
      'Открыть в новой вкладке',
      'Эмодзи',
      'Блок кода',
      'Язык кода',
      'Копировать код',
      'Таблица',
      'Операции с таблицей',
    ]);
    expect(new Set(names).size).toBe(names.length);
  });

  it('never offers a heading level outside 3 and 4', () => {
    const ids = TOOLBAR_GROUPS.flatMap((group) => group.controls.map((control) => control.id));

    expect(ids.filter((id) => /^h\d$/.test(id))).toEqual(['h3', 'h4']);
  });
});

describe('formatShortcut', () => {
  it('writes Ctrl where a PC has it and the symbols where a Mac has them', () => {
    expect(formatShortcut('Mod+B', false)).toBe('Ctrl+B');
    expect(formatShortcut('Mod+Shift+Z', false)).toBe('Ctrl+Shift+Z');
    expect(formatShortcut('Mod+B', true)).toBe('⌘B');
    expect(formatShortcut('Mod+Shift+Z', true)).toBe('⌘⇧Z');
  });
});
