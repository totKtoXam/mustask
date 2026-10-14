import type { UserCandidate, UserCandidates } from '../api/membershipApi';
import { userSearchTexts } from './memberTexts';

/** The search starts at 2 characters, after trimming (mod-014 BR-UP-14, design § 3.5). */
export const USER_SEARCH_MIN_LENGTH = 2;

/** The field's text is at most 100 characters (mod-014 BR-UP-14). */
export const USER_SEARCH_MAX_LENGTH = 100;

/** One line of the candidate list: a user, or a hint that cannot be chosen (design § 3.5). */
export type UserOption = { kind: 'user'; user: UserCandidate } | { kind: 'hint'; text: string };

/** Where the search stands for the text typed now. */
export type UserSearch =
  | { status: 'tooShort' }
  | { status: 'searching' }
  | { status: 'failed' }
  | { status: 'done'; result: UserCandidates };

/** The text as the search sends it: trimmed; under 2 characters nothing is asked. */
export function searchTextOf(typed: string): string {
  return typed.trim();
}

export function isSearchable(text: string): boolean {
  return text.length >= USER_SEARCH_MIN_LENGTH;
}

function hint(text: string): UserOption {
  return { kind: 'hint', text };
}

/**
 * The lines the list shows, one hint at a time (design § 3.5): the hint for a
 * short text, «Поиск…» while asking, the failure, «Пользователи не найдены»,
 * or up to 20 users with «Показаны первые 20. Уточните запрос.» last when more
 * matched. The users are the server's answer as it is.
 */
export function userOptions(search: UserSearch): UserOption[] {
  switch (search.status) {
    case 'tooShort':
      return [hint(userSearchTexts.tooShort)];
    case 'searching':
      return [hint(userSearchTexts.searching)];
    case 'failed':
      return [hint(userSearchTexts.failed)];
    case 'done': {
      const { users, moreMatched } = search.result;
      if (users.length === 0) {
        return [hint(userSearchTexts.noMatch)];
      }
      const lines: UserOption[] = users.map((user): UserOption => ({ kind: 'user', user }));
      if (moreMatched) {
        lines.push(hint(userSearchTexts.moreMatched));
      }
      return lines;
    }
  }
}

/** The line's text; the field shows it for the chosen user. */
export function optionLabel(option: UserOption): string {
  return option.kind === 'user' ? option.user.displayName : option.text;
}

/** A stable key per line: users by id, so two equal names never collide. */
export function optionKey(option: UserOption): string {
  return option.kind === 'user' ? `user:${option.user.id}` : `hint:${option.text}`;
}

export function sameOption(first: UserOption, second: UserOption): boolean {
  return optionKey(first) === optionKey(second);
}
