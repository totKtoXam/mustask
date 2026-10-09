import { describe, expect, it } from 'vitest';
import { sampleProcessDefinition } from '../../process/testFixtures';
import type { WorkItemRef } from '../api/workItemsApi';
import {
  NO_PARENT_OPTION,
  parentOptionKey,
  parentOptionLabel,
  parentOptionOf,
  parentOptions,
  sameParentOption,
  workItemRefLabel,
} from './parentOptions';
import { allowedParentTypes, parentHelperText } from './process';

const FEAT_6: WorkItemRef = {
  id: 'feat-6',
  identifier: 'MSK-FEAT-6',
  type: { id: 'type-feat', key: 'FEAT', name: 'Фича', appearance: 'feature' },
  title: 'Вход по ссылке',
  state: { id: 'state-new', name: 'Новая', category: { code: 'Proposed', label: 'Новые' } },
};

const labels = (options: ReturnType<typeof parentOptions>) => options.map(parentOptionLabel);

describe('«Родитель»’s lines (feat-001 spec § Screens and copy)', () => {
  it('puts «Нет родителя» first while searching and after a failure', () => {
    expect(labels(parentOptions({ status: 'searching' }))).toEqual(['Нет родителя', 'Поиск…']);
    expect(labels(parentOptions({ status: 'failed' }))).toEqual([
      'Нет родителя',
      'Не удалось выполнить поиск. Попробуйте ещё раз.',
    ]);
  });

  it('says «Ничего не найдено» for no match', () => {
    expect(
      labels(parentOptions({ status: 'done', result: { items: [], moreMatched: false } })),
    ).toEqual(['Нет родителя', 'Ничего не найдено']);
  });

  it('lists the items as «<ID> <Заголовок>», and the limit’s hint last when more matched', () => {
    const options = parentOptions({
      status: 'done',
      result: { items: [FEAT_6], moreMatched: true },
    });
    expect(labels(options)).toEqual([
      'Нет родителя',
      'MSK-FEAT-6 Вход по ссылке',
      'Показаны первые 20. Уточните запрос.',
    ]);
    expect(options.at(-1)?.kind).toBe('hint');
    expect(
      labels(parentOptions({ status: 'done', result: { items: [FEAT_6], moreMatched: false } })),
    ).toEqual(['Нет родителя', 'MSK-FEAT-6 Вход по ссылке']);
  });

  it('leaves «Нет родителя» out for the backlog’s dialog (feat-001 US-5), every other line kept', () => {
    const without = { none: false };
    expect(labels(parentOptions({ status: 'searching' }, without))).toEqual(['Поиск…']);
    expect(labels(parentOptions({ status: 'failed' }, without))).toEqual([
      'Не удалось выполнить поиск. Попробуйте ещё раз.',
    ]);
    expect(
      labels(parentOptions({ status: 'done', result: { items: [], moreMatched: false } }, without)),
    ).toEqual(['Ничего не найдено']);
    expect(
      labels(
        parentOptions({ status: 'done', result: { items: [FEAT_6], moreMatched: true } }, without),
      ),
    ).toEqual(['MSK-FEAT-6 Вход по ссылке', 'Показаны первые 20. Уточните запрос.']);
  });

  it('reads the value as an option: «Нет родителя» or the item', () => {
    expect(parentOptionOf(null)).toBe(NO_PARENT_OPTION);
    expect(parentOptionLabel(parentOptionOf(FEAT_6))).toBe('MSK-FEAT-6 Вход по ссылке');
    expect(workItemRefLabel(FEAT_6)).toBe('MSK-FEAT-6 Вход по ссылке');
  });

  it('keys an item by id, so the same item with another title is the same option', () => {
    expect(parentOptionKey(parentOptionOf(FEAT_6))).toBe('item:feat-6');
    expect(
      sameParentOption(parentOptionOf(FEAT_6), parentOptionOf({ ...FEAT_6, title: 'Другой' })),
    ).toBe(true);
    expect(sameParentOption(parentOptionOf(FEAT_6), NO_PARENT_OPTION)).toBe(false);
  });
});

describe('allowedParentTypes and the helper (feat-001 BR-H-2; US-1 «Each type is offered the parents its level allows»)', () => {
  const typeId = (key: string) =>
    sampleProcessDefinition.workItemTypes.find((type) => type.key === key)!.id;
  const names = (key: string) =>
    allowedParentTypes(sampleProcessDefinition, typeId(key)).map((type) => type.name);

  it.each([
    ['EPIC', ['Эпик']],
    ['FEAT', ['Эпик', 'Фича']],
    ['US', ['Эпик', 'Фича', 'Пользовательская история', 'Баг']],
    ['BUG', ['Эпик', 'Фича', 'Пользовательская история', 'Баг']],
    ['TASK', ['Эпик', 'Фича', 'Пользовательская история', 'Баг', 'Задача']],
  ])('%s may have %j, in the process’s display order', (key, expected) => {
    expect(names(key)).toEqual(expected);
  });

  it('reads «Родителем может быть: Эпик, Фича, Пользовательская история, Баг.» for a story', () => {
    expect(parentHelperText(allowedParentTypes(sampleProcessDefinition, typeId('US')))).toBe(
      'Родителем может быть: Эпик, Фича, Пользовательская история, Баг.',
    );
  });

  it('names nothing for a type the read does not hold', () => {
    expect(allowedParentTypes(sampleProcessDefinition, 'unknown')).toEqual([]);
    expect(parentHelperText([])).toBeNull();
  });
});
