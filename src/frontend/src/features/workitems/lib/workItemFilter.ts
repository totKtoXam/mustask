// The list's four filter dimensions as feat-003 fixes them (spec BR-WS-1..-14; design §§ 2-6): the
// address form, the words, the options of «Статус» and the judgement of a new item against the
// filter. The team's own address form is mod-020's (`hooks/useTeamFilter.ts`), imported here.
import type { ProcessDefinitionResponse } from '../../process';
import {
  NO_ASSIGNEE_FILTER_VALUE,
  type WorkItem,
  type WorkItemListFilter,
} from '../api/workItemsApi';
import { TEAM_FILTER_PARAM, teamFilterOf } from '../hooks/useTeamFilter';
import { NOT_IN_FILTER_REASON, inFilter } from './teams';

/** The address parameters of the other three dimensions, named as the list's read names them (api-contract § 2.1). */
export const STATE_FILTER_PARAM = 'state';
export const ASSIGNEE_FILTER_PARAM = 'assigneeId';
export const SEARCH_FILTER_PARAM = 'search';

const FILTER_PARAMS = [
  STATE_FILTER_PARAM,
  ASSIGNEE_FILTER_PARAM,
  SEARCH_FILTER_PARAM,
  TEAM_FILTER_PARAM,
] as const;

/** The keyword's longest text: the title's limit (BR-M3-3, BR-WS-9). */
export const SEARCH_MAX_LENGTH = 255;

/** The words of the band's new fields, final in design §§ 2, 5, 6 and 10. */
export const filterTexts = {
  searchLabel: 'Поиск',
  /** The hint, and the text at rest while nothing is typed. */
  searchHint: 'Заголовок или ID',
  stateLabel: 'Статус',
  allStates: 'Все статусы',
  stateFailed: 'Не удалось загрузить статусы. Попробуйте ещё раз.',
  assigneeLabel: 'Исполнитель',
  allAssignees: 'Все исполнители',
  assigneeFailed: 'Не удалось загрузить исполнителей. Попробуйте ещё раз.',
  noAssignees: 'Никого не найдено',
  loading: 'Загрузка…',
  noMatch: 'Ничего не найдено',
  /** In place of «Все …» when the address holds a value no option can name (D-4). */
  set: 'Фильтр задан',
  /** What the page announces after a change of a filter settles (design § 9). */
  found: (count: number): string => `Найдено: ${count}`,
  /** The creation notice's reason (design § 6; the same words as mod-017 § 6.5). */
  notInFilter: NOT_IN_FILTER_REASON,
} as const;

/** The values of an address parameter, in its order, each once; a blank value names nothing and is left out (BR-WS-11). */
function valuesOf(params: URLSearchParams, name: string): string[] {
  return [...new Set(params.getAll(name).filter((value) => value.trim() !== ''))];
}

/**
 * The filter an address carries (BR-WS-11). A blank value is left out, the same value twice counts
 * once and any other value is kept as written, whether it names anything or not (BR-WS-10). The
 * keyword is the first non-blank `search`, trimmed. The team part is mod-020's.
 */
export function filterOf(search: string): WorkItemListFilter {
  const params = new URLSearchParams(search);
  const keyword =
    params
      .getAll(SEARCH_FILTER_PARAM)
      .map((value) => value.trim())
      .find((value) => value !== '') ?? '';
  return {
    state: valuesOf(params, STATE_FILTER_PARAM),
    assigneeId: valuesOf(params, ASSIGNEE_FILTER_PARAM),
    search: keyword,
    teamId: teamFilterOf(search),
  };
}

/**
 * `search` (an address's query string, with or without the leading `?`) with the four dimensions
 * replaced by `filter`: every other parameter stays where it was, the four stand after them in the
 * order state, assignee, keyword, team (design § 3). Nothing is left but `''` when nothing remains.
 */
export function withFilter(search: string, filter: WorkItemListFilter): string {
  const params = new URLSearchParams(search);
  for (const name of FILTER_PARAMS) {
    params.delete(name);
  }
  for (const name of new Set(filter.state.filter((value) => value.trim() !== ''))) {
    params.append(STATE_FILTER_PARAM, name);
  }
  for (const id of new Set(filter.assigneeId.filter((value) => value.trim() !== ''))) {
    params.append(ASSIGNEE_FILTER_PARAM, id);
  }
  if (filter.search.trim() !== '') {
    params.append(SEARCH_FILTER_PARAM, filter.search.trim());
  }
  for (const id of new Set(filter.teamId.filter((value) => value !== ''))) {
    params.append(TEAM_FILTER_PARAM, id);
  }
  const text = params.toString();
  return text === '' ? '' : `?${text}`;
}

/**
 * Whether any dimension holds a value (design § 5): one that names nothing is a filter that is on.
 * A blank keyword sets none (BR-WS-9).
 */
export function isFiltered(filter: WorkItemListFilter): boolean {
  return (
    filter.state.length > 0 ||
    filter.assigneeId.length > 0 ||
    filter.search !== '' ||
    filter.teamId.length > 0
  );
}

/**
 * The filter's content as one comparable text: the values of each dimension as a set (the order
 * they were chosen in does not count), the keyword trimmed. The page number belongs to it, so any
 * change of content shows page 1 (BR-WS-11) and the same content, however its arrays were made,
 * keeps the page.
 */
export function filterContent(filter: WorkItemListFilter): string {
  return JSON.stringify({
    state: [...filter.state].sort(),
    assigneeId: [...filter.assigneeId].sort(),
    search: filter.search,
    teamId: [...filter.teamId].sort(),
  });
}

/**
 * The options of «Статус» (design § 2.2, BR-WS-3): the state names of the process, each once, in
 * the order they first appear across its types — names only, the panel keeps no list of deleted
 * names (the server judges by category, BR-WS-4).
 */
export function stateOptions(
  process: Pick<ProcessDefinitionResponse, 'workItemTypes'>,
): { value: string; label: string }[] {
  const names = new Set<string>();
  for (const type of process.workItemTypes) {
    for (const state of type.states) {
      names.add(state.name);
    }
  }
  return [...names].map((name) => ({ value: name, label: name }));
}

/** Lower case, «ё» read as «е»: how the server compares a keyword with a title (BR-WS-9). */
function fold(text: string): string {
  return text.toLocaleLowerCase('ru').replace(/ё/g, 'е');
}

/**
 * The characters no title can hold, so a keyword with one matches nothing and is never an error
 * (BR-WS-9): U+0000 and the line breaks (the title's set, `rules.ts`), by code so no control
 * character is written in a pattern.
 */
const UNSEARCHABLE_CHARACTERS = [0x00, 0x0a, 0x0b, 0x0c, 0x0d, 0x85, 0x2028, 0x2029].map((code) =>
  String.fromCharCode(code),
);
/** An identifier is printable ASCII (the server parses ASCII-only text, upper-cased). */
const PRINTABLE_ASCII = /^[ -~]+$/;
const NUMBER_TEXT = /^[1-9][0-9]{0,9}$/;
const INT_MAX = 2_147_483_647;

/**
 * Whether the keyword finds the item (BR-WS-9): its title contains it, ignoring letter case and
 * «ё»; or it is the item's identifier, in any letter case; or it is the item's number, in digits.
 * The list's meaning, in one pure function, so a creation is never told it fits what the next read
 * would not show (BR-WS-14).
 */
export function matchesKeyword(
  item: Pick<WorkItem, 'identifier' | 'number' | 'title'>,
  keyword: string,
): boolean {
  const text = keyword.trim();
  if (text === '') {
    return true;
  }
  if (
    text.length > SEARCH_MAX_LENGTH ||
    UNSEARCHABLE_CHARACTERS.some((character) => text.includes(character))
  ) {
    return false;
  }
  if (fold(item.title).includes(fold(text))) {
    return true;
  }
  if (PRINTABLE_ASCII.test(text) && text.toUpperCase() === item.identifier) {
    return true;
  }
  return NUMBER_TEXT.test(text) && Number(text) <= INT_MAX && Number(text) === item.number;
}

/**
 * Whether a new item falls outside the filter that is set (BR-WS-14), judged on the item the
 * create returns: any dimension that is set and does not take it. «Новая» is the state every new
 * item starts in, so a state filter that leaves «Новая» out hides it.
 */
export function filterMismatch(
  item: Pick<WorkItem, 'identifier' | 'number' | 'title' | 'state' | 'assignee' | 'team'>,
  filter: WorkItemListFilter,
): boolean {
  if (filter.state.length > 0 && !filter.state.includes(item.state.name)) {
    return true;
  }
  if (filter.assigneeId.length > 0) {
    const taken =
      item.assignee === null
        ? filter.assigneeId.includes(NO_ASSIGNEE_FILTER_VALUE)
        : filter.assigneeId.includes(item.assignee.id);
    if (!taken) {
      return true;
    }
  }
  if (filter.teamId.length > 0 && !inFilter(item.team, filter.teamId)) {
    return true;
  }
  return filter.search !== '' && !matchesKeyword(item, filter.search);
}

/**
 * The notice after a creation (design § 6): «Рабочий элемент MSK-BUG-43 создан.», and the reason
 * the list does not show it when it fails any of the four dimensions that are set.
 */
export function createdInListNotice(
  item: Pick<WorkItem, 'identifier' | 'number' | 'title' | 'state' | 'assignee' | 'team'>,
  filter: WorkItemListFilter,
): string {
  const created = `Рабочий элемент ${item.identifier} создан.`;
  return filterMismatch(item, filter) ? `${created} ${filterTexts.notInFilter}` : created;
}
