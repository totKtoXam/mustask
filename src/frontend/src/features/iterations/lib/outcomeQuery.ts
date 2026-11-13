import type { OutcomeLevel, OutcomeQuery } from '../api/iterationsApi';

export const OUTCOME_LEVELS: readonly OutcomeLevel[] = ['epics', 'features', 'stories'];
export const DEFAULT_LEVEL: OutcomeLevel = 'stories';

const DATE = /^\d{4}-\d{2}-\d{2}$/;

function isDate(value: string | null): value is string {
  if (value === null || !DATE.test(value)) return false;
  return !Number.isNaN(Date.parse(`${value}T00:00:00Z`));
}

/**
 * The outcome's address query (`?level=&from=&to=`, design § 6.2): an unusable level is the
 * default «Истории»; a half-filled, unparsable or reversed range is no range (the server's
 * default applies), never an error.
 */
export function parseOutcomeQuery(search: URLSearchParams): OutcomeQuery {
  const rawLevel = search.get('level');
  const level = OUTCOME_LEVELS.find((candidate) => candidate === rawLevel) ?? DEFAULT_LEVEL;
  const from = search.get('from');
  const to = search.get('to');
  if (isDate(from) && isDate(to) && to >= from) {
    return { level, from, to };
  }
  return { level };
}

/** The address query of a query: the default level and no range write nothing. */
export function outcomeSearch(query: OutcomeQuery): string {
  const params = new URLSearchParams();
  if (query.level !== DEFAULT_LEVEL) params.set('level', query.level);
  if (query.from && query.to) {
    params.set('from', query.from);
    params.set('to', query.to);
  }
  const text = params.toString();
  return text ? `?${text}` : '';
}
