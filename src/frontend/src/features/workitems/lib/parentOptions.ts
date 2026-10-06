import type { ParentCandidates, WorkItemRef } from '../api/workItemsApi';

/** «Родитель»'s lines other than items (feat-001 spec § Screens and copy). */
export const parentPickerTexts = {
  none: 'Нет родителя',
  searching: 'Поиск…',
  noMatch: 'Ничего не найдено',
  moreMatched: 'Показаны первые 20. Уточните запрос.',
  failed: 'Не удалось выполнить поиск. Попробуйте ещё раз.',
} as const;

/** One line of «Родитель»'s list: «Нет родителя», an item, or a hint that cannot be chosen. */
export type ParentOption =
  { kind: 'none' } | { kind: 'item'; item: WorkItemRef } | { kind: 'hint'; text: string };

export const NO_PARENT_OPTION: ParentOption = { kind: 'none' };

/** Where the search stands for the text typed now. */
export type ParentSearch =
  { status: 'searching' } | { status: 'failed' } | { status: 'done'; result: ParentCandidates };

function hint(text: string): ParentOption {
  return { kind: 'hint', text };
}

/**
 * The lines the list shows: «Нет родителя» always first, then «Поиск…» while
 * asking, the failure, «Ничего не найдено», or up to 20 items with «Показаны
 * первые 20. Уточните запрос.» last when more matched (api-contract § 3.4).
 */
export function parentOptions(search: ParentSearch): ParentOption[] {
  switch (search.status) {
    case 'searching':
      return [NO_PARENT_OPTION, hint(parentPickerTexts.searching)];
    case 'failed':
      return [NO_PARENT_OPTION, hint(parentPickerTexts.failed)];
    case 'done': {
      const { items, moreMatched } = search.result;
      if (items.length === 0) {
        return [NO_PARENT_OPTION, hint(parentPickerTexts.noMatch)];
      }
      const lines: ParentOption[] = [
        NO_PARENT_OPTION,
        ...items.map((item): ParentOption => ({ kind: 'item', item })),
      ];
      if (moreMatched) {
        lines.push(hint(parentPickerTexts.moreMatched));
      }
      return lines;
    }
  }
}

/** «<ID> <Заголовок>»: how an item reads in the list, in the field and in the children list. */
export function workItemRefLabel(item: Pick<WorkItemRef, 'identifier' | 'title'>): string {
  return `${item.identifier} ${item.title}`;
}

/** The option the value is: «Нет родителя» or that item. */
export function parentOptionOf(parent: WorkItemRef | null): ParentOption {
  return parent === null ? NO_PARENT_OPTION : { kind: 'item', item: parent };
}

/** The line's text; the field shows it for the chosen one. */
export function parentOptionLabel(option: ParentOption): string {
  switch (option.kind) {
    case 'none':
      return parentPickerTexts.none;
    case 'item':
      return workItemRefLabel(option.item);
    case 'hint':
      return option.text;
  }
}

/** A stable key per line: items by id. */
export function parentOptionKey(option: ParentOption): string {
  switch (option.kind) {
    case 'none':
      return 'none';
    case 'item':
      return `item:${option.item.id}`;
    case 'hint':
      return `hint:${option.text}`;
  }
}

export function sameParentOption(first: ParentOption, second: ParentOption): boolean {
  return parentOptionKey(first) === parentOptionKey(second);
}
