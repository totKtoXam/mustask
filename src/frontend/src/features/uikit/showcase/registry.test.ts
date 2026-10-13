import { describe, expect, it } from 'vitest';
import { showcaseRegistry } from './registry';

// tech-015 design § 10.3, § 12 (part 8): the registry is the showcase's table of contents.

const TITLES = [
  'Цвета',
  'Шрифт и размеры текста',
  'Размеры и отступы',
  'Значки',
  'Кнопки',
  'Меню',
  'Типы, состояния и люди',
  'Теги',
  'Длинный текст',
  'Заголовок страницы',
  'Вкладки карточки',
  'Поля ввода',
  'Разделы и колонки',
  'Карточка',
  'Окна',
  'Уведомления',
  'Пустые, ошибочные и загружаемые состояния',
];

describe('tech-015 Scenario: the showcase registry (design § 10.3)', () => {
  it('lists the sections of the design in its order, the table family’s left to tech-016 (D-6)', () => {
    expect(showcaseRegistry.map((section) => section.title)).toEqual(TITLES);
    expect(showcaseRegistry.map((section) => section.id)).not.toContain('tables');
  });

  it('gives every section a unique id that is a valid anchor', () => {
    const ids = showcaseRegistry.map((section) => section.id);
    expect(new Set(ids).size).toBe(ids.length);
    for (const id of ids) {
      expect(id).toMatch(/^[a-z][a-z-]*$/);
    }
  });

  it('gives every section a title, a usage sentence and a demo', () => {
    for (const section of showcaseRegistry) {
      expect(section.title.trim()).not.toBe('');
      expect(section.usage.trim()).toMatch(/[.!]$/);
      expect(section.Demo).toBeTypeOf('function');
    }
  });

  it('shows a component in one section only', () => {
    const covered = showcaseRegistry.flatMap((section) => section.covers);
    expect(new Set(covered).size).toBe(covered.length);
  });

  it('covers no component in the token sections', () => {
    for (const id of ['colours', 'typography', 'sizes']) {
      expect(showcaseRegistry.find((section) => section.id === id)?.covers).toEqual([]);
    }
  });
});
