import type { AssigneeCandidates, WorkItemAssignee } from '../api/workItemsApi';
import { UNASSIGNED_LABEL, assigneeLabel } from './assignee';

/** BR-M3-22: the search starts at 2 characters. */
export const ASSIGNEE_SEARCH_MIN_LENGTH = 2;

export const assigneePickerTexts = {
  tooShort: 'Введите не менее 2 символов имени или почты',
  searching: 'Поиск…',
  noMatch: 'Никого не найдено',
  moreMatched: 'Показаны первые 20. Уточните запрос.',
  failed: 'Не удалось выполнить поиск. Попробуйте ещё раз.',
} as const;

/**
 * One line of «Исполнитель»'s list: «Не назначен», a user, or a hint that
 * cannot be chosen (spec § Pickers).
 */
export type AssigneeOption =
  { kind: 'none' } | { kind: 'user'; user: WorkItemAssignee } | { kind: 'hint'; text: string };

export const NONE_OPTION: AssigneeOption = { kind: 'none' };

/** Where the search stands for the text typed now. */
export type AssigneeSearch =
  | { status: 'tooShort' }
  | { status: 'searching' }
  | { status: 'failed' }
  | { status: 'done'; result: AssigneeCandidates };

/** The text as the search sends it: trimmed; under 2 characters nothing is asked. */
export function searchTextOf(typed: string): string {
  return typed.trim();
}

export function isSearchable(text: string): boolean {
  return text.length >= ASSIGNEE_SEARCH_MIN_LENGTH;
}

function hint(text: string): AssigneeOption {
  return { kind: 'hint', text };
}

/**
 * The lines the list shows (spec § Pickers): «Не назначен» always first, then
 * the hint for a short text, «Поиск…» while asking, the failure, «Никого не
 * найдено», or up to 20 users with «Показаны первые 20. Уточните запрос.» last
 * when more matched.
 */
export function assigneeOptions(search: AssigneeSearch): AssigneeOption[] {
  switch (search.status) {
    case 'tooShort':
      return [NONE_OPTION, hint(assigneePickerTexts.tooShort)];
    case 'searching':
      return [NONE_OPTION, hint(assigneePickerTexts.searching)];
    case 'failed':
      return [NONE_OPTION, hint(assigneePickerTexts.failed)];
    case 'done': {
      const { users, moreMatched } = search.result;
      if (users.length === 0) {
        return [NONE_OPTION, hint(assigneePickerTexts.noMatch)];
      }
      const lines: AssigneeOption[] = [
        NONE_OPTION,
        ...users.map((user): AssigneeOption => ({ kind: 'user', user })),
      ];
      if (moreMatched) {
        lines.push(hint(assigneePickerTexts.moreMatched));
      }
      return lines;
    }
  }
}

/** The option the current value is: «Не назначен» or that user. */
export function optionOf(assignee: WorkItemAssignee | null): AssigneeOption {
  return assignee === null ? NONE_OPTION : { kind: 'user', user: assignee };
}

/** The line's text; the field shows it for the chosen one. */
export function optionLabel(option: AssigneeOption): string {
  switch (option.kind) {
    case 'none':
      return UNASSIGNED_LABEL;
    case 'user':
      return assigneeLabel(option.user);
    case 'hint':
      return option.text;
  }
}

/** A stable key per line: users by id, so two equal names never collide. */
export function optionKey(option: AssigneeOption): string {
  switch (option.kind) {
    case 'none':
      return 'none';
    case 'user':
      return `user:${option.user.id}`;
    case 'hint':
      return `hint:${option.text}`;
  }
}

export function sameOption(first: AssigneeOption, second: AssigneeOption): boolean {
  return optionKey(first) === optionKey(second);
}
