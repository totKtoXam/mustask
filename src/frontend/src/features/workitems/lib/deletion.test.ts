import { describe, expect, it } from 'vitest';
import type { WorkItemDeletion } from '../api/workItemsApi';
import { deletionTexts } from './cardTexts';
import { deletionLine, isDeleted, userNameWithMarks } from './deletion';

// mod-005 design § 4 (the deletion line and its marks), § 3 and § 6 (the sentences), BR-DR-1 (the
// category decides) and BR-13 (the marks).

const BY = { id: 'anna', displayName: 'Анна Смирнова', isActive: true, isMember: true };
const FROM = {
  id: 'state-active',
  name: 'Активна',
  category: { code: 'InProgress', label: 'В работе' },
};

function deletionBy(by: Partial<typeof BY>): WorkItemDeletion {
  return { fromState: FROM, by: { ...BY, ...by }, at: '2026-10-18T09:14:00+00:00' };
}

describe('isDeleted (BR-DR-1: the category decides, never the name)', () => {
  const state = (name: string, code: string) => ({
    state: { id: 's', name, category: { code, label: '' } },
  });

  it('is true for a state in the Removed category whatever it is called', () => {
    expect(isDeleted(state('Удалена', 'Removed'))).toBe(true);
    expect(isDeleted(state('Корзина', 'Removed'))).toBe(true);
  });

  it('is false for a state called «Удалена» outside the category, and for every other category', () => {
    expect(isDeleted(state('Удалена', 'InProgress'))).toBe(false);
    expect(isDeleted(state('Новая', 'Proposed'))).toBe(false);
    expect(isDeleted(state('Закрыта', 'Completed'))).toBe(false);
  });
});

describe('userNameWithMarks (BR-13)', () => {
  it('is the display name of a known, active member', () => {
    expect(userNameWithMarks(BY)).toBe('Анна Смирнова');
  });

  it('adds «неактивен» for an account that is disabled or deleted', () => {
    expect(userNameWithMarks({ ...BY, isActive: false })).toBe('Анна Смирнова (неактивен)');
  });

  it('adds «не в организации» for a user who left, which prevails over «неактивен»', () => {
    expect(userNameWithMarks({ ...BY, isMember: false })).toBe('Анна Смирнова (не в организации)');
    expect(userNameWithMarks({ ...BY, isMember: false, isActive: false })).toBe(
      'Анна Смирнова (не в организации)',
    );
  });

  it('names a user the directory does not know «Неизвестный пользователь», without «неактивен»', () => {
    // The contract flags an unknown user inactive; the name already says nobody knows them.
    expect(userNameWithMarks({ displayName: null, isActive: false, isMember: true })).toBe(
      'Неизвестный пользователь',
    );
    expect(userNameWithMarks({ displayName: null, isActive: false, isMember: false })).toBe(
      'Неизвестный пользователь (не в организации)',
    );
  });
});

describe('deletionLine (BR-DR-7)', () => {
  it('reads «Удалил(а): <имя>, <ДД.ММ.ГГГГ ЧЧ:ММ>»', () => {
    const line = deletionLine(deletionBy({}));

    expect(line).toMatch(/^Удалил\(а\): Анна Смирнова, \d{2}\.\d{2}\.\d{4} \d{2}:\d{2}$/);
  });

  it('carries the mark after the name', () => {
    expect(deletionLine(deletionBy({ isActive: false }))).toContain('Анна Смирнова (неактивен), ');
  });

  it('is none for an item with no marks: no line, and «Восстановить» stays', () => {
    expect(deletionLine(null)).toBeNull();
  });
});

describe('the sentences of the three dialogs (design § 3, § 5, § 6)', () => {
  it('Delete: always the first part, the children’s part with live children, the draft’s with a draft', () => {
    const text = deletionTexts.delete.text;

    expect(text('MSK-BUG-7', 0, false)).toBe(
      'Рабочий элемент MSK-BUG-7 получит статус «Удалена». Его можно будет восстановить.',
    );
    expect(text('MSK-US-12', 1, false)).toBe(
      'Рабочий элемент MSK-US-12 получит статус «Удалена». Его можно будет восстановить. Дочерние элементы (1) не удаляются: пока он удалён, в бэклогах они показываются без родителя.',
    );
    expect(text('MSK-US-12', 1, true)).toBe(
      'Рабочий элемент MSK-US-12 получит статус «Удалена». Его можно будет восстановить. Дочерние элементы (1) не удаляются: пока он удалён, в бэклогах они показываются без родителя. Несохранённые изменения будут потеряны.',
    );
    expect(text('MSK-BUG-7', 0, true)).toBe(
      'Рабочий элемент MSK-BUG-7 получит статус «Удалена». Его можно будет восстановить. Несохранённые изменения будут потеряны.',
    );
  });

  it('Restore: the sentence, the deleted parent’s and the notices', () => {
    const texts = deletionTexts.restoreDialog;

    expect(texts.text('MSK-US-12')).toBe(
      'Рабочий элемент MSK-US-12 вернётся в списки, на доски и в бэклоги.',
    );
    expect(texts.deletedParent('MSK-EPIC-1')).toBe(
      'Родитель MSK-EPIC-1 удалён: пока он не восстановлен, в бэклогах рабочий элемент показывается без родителя.',
    );
    expect(texts.done('MSK-US-12')).toBe('Рабочий элемент MSK-US-12 восстановлен.');
    expect(deletionTexts.delete.done('MSK-US-12')).toBe('Рабочий элемент MSK-US-12 удалён.');
  });

  it('Purge: names the title, adds the children’s sentence only with live children', () => {
    const texts = deletionTexts.purgeDialog;

    expect(texts.text('MSK-BUG-7', 'Падение при пустом заголовке', 0)).toBe(
      'Рабочий элемент MSK-BUG-7 «Падение при пустом заголовке» будет удалён без возможности восстановления.',
    );
    expect(texts.text('MSK-EPIC-1', 'Вход и регистрация', 2)).toBe(
      'Рабочий элемент MSK-EPIC-1 «Вход и регистрация» будет удалён без возможности восстановления. Дочерние элементы (2) останутся без родителя.',
    );
    expect(texts.done('MSK-BUG-7')).toBe('Рабочий элемент MSK-BUG-7 удалён навсегда.');
  });
});
