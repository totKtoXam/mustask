// The fields per type as the form holds, compares, checks and sends them
// (feat-022 spec § Controls and copy, § Saving; BR-M3-31..BR-M3-38;
// api-contract § 3.2). The form holds each value as its control does — a
// list's key, a number's text, a date's `YYYY-MM-DD`, a text — and the save
// sends exactly the fields whose value differs from the loaded one. The
// browser's checks spare a round trip; the server runs the same rules and has
// the last word.
import type {
  FieldValue,
  SaveFieldsBody,
  WorkItem,
  WorkItemFieldName,
  WorkItemFields,
} from '../api/workItemsApi';
import {
  LIST_FIELDS,
  NUMBER_FIELDS,
  WORK_ITEM_FIELD_NAMES,
  fieldsOf,
  kindOf,
  type DateFieldName,
} from './fields';
import { ruleTexts } from './rules';

/**
 * Every field as its control holds it: a list's key (`""` for «Не указан»), a
 * number's text as typed, a date's `YYYY-MM-DD` (`""` when empty, or
 * `INCOMPLETE_DATE`), a text as typed.
 */
export type FieldsDraft = Record<WorkItemFieldName, string>;

/**
 * A date typed only in part: the browser's date field then holds no value but
 * reports a bad input. Never a `YYYY-MM-DD`, so never a value of its own.
 */
export const INCOMPLETE_DATE = 'incomplete';

/** BR-M3-32: a number runs from 0 to 9 999… */
export const NUMBER_MAX = 9_999;
/** …with at most two digits after the separator. */
export const NUMBER_DECIMALS = 2;
/** BR-M3-36: the type texts' length, in UTF-16 code units. */
export const TEXT_MAX_LENGTH = 100_000;
/** BR-M3-35: the first and the last date a date field takes. */
export const EARLIEST_DATE = '2000-01-01';
export const LATEST_DATE = '2099-12-31';

/** Digits with at most one «,» or «.», a leading minus kept so a range check can speak. */
const NUMBER_TEXT = /^-?(?:\d+(?:[.,]\d*)?|[.,]\d+)$/;
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

/** A stored number as the form shows it: with «,» and no trailing zeros (BR-M3-32), `""` when empty. */
export function formatNumber(value: number | null): string {
  return value === null ? '' : String(value).replace('.', ',');
}

/** A number field's text: empty, a number with its significant decimals, or not a number. */
export type ParsedNumber =
  { kind: 'empty' } | { kind: 'number'; value: number; decimals: number } | { kind: 'invalid' };

/**
 * Reads a number field's text (spec § Controls and copy): spaces at either end
 * ignored, «,» or «.» as the separator. Its decimals are counted without
 * trailing zeros, so «2,50» is the 2,5 the server stores (api-contract § 3.2).
 */
export function parseNumber(text: string): ParsedNumber {
  const trimmed = text.trim();
  if (trimmed === '') {
    return { kind: 'empty' };
  }
  if (!NUMBER_TEXT.test(trimmed)) {
    return { kind: 'invalid' };
  }
  const normalized = trimmed.replace(',', '.');
  const fraction = (normalized.split('.')[1] ?? '').replace(/0+$/, '');
  return { kind: 'number', value: Number(normalized), decimals: fraction.length };
}

/** The first reason a number field's text is refused, in the server's order, or `null`. */
export function numberError(text: string, integer: boolean): string | null {
  const parsed = parseNumber(text);
  if (parsed.kind === 'empty') {
    return null;
  }
  if (parsed.kind === 'invalid') {
    return ruleTexts.notANumber;
  }
  if (parsed.value < 0 || parsed.value > NUMBER_MAX) {
    return ruleTexts.numberOutOfRange;
  }
  if (integer) {
    return parsed.decimals > 0 ? ruleTexts.integerRequired : null;
  }
  return parsed.decimals > NUMBER_DECIMALS ? ruleTexts.numberTooPrecise : null;
}

/** A date field's reason, or `null`: typed in part, or outside 2000–2099 (BR-M3-35). */
export function dateError(value: string): string | null {
  if (value === INCOMPLETE_DATE) {
    return ruleTexts.dateIncomplete;
  }
  if (value === '') {
    return null;
  }
  return ISO_DATE.test(value) && value >= EARLIEST_DATE && value <= LATEST_DATE
    ? null
    : ruleTexts.dateOutOfRange;
}

/** A type text's reason, or `null`: only its length (BR-M3-36). */
export function textError(value: string): string | null {
  return value.length > TEXT_MAX_LENGTH ? ruleTexts.textTooLong : null;
}

/** The fields as the form shows them, from the item as loaded or last saved. */
export function fieldsDraftOf(fields: WorkItemFields): FieldsDraft {
  const draft = {} as FieldsDraft;
  for (const name of WORK_ITEM_FIELD_NAMES) {
    const stored = fields[name];
    draft[name] =
      kindOf(name) === 'number'
        ? formatNumber(stored as number | null)
        : stored === null
          ? ''
          : String(stored);
  }
  return draft;
}

/** Whether `name`'s held value differs from the stored one — numbers as numbers (BR-M3-38). */
function differs(name: WorkItemFieldName, held: string, stored: WorkItemFields[WorkItemFieldName]) {
  switch (kindOf(name)) {
    case 'number': {
      const parsed = parseNumber(held);
      if (parsed.kind === 'empty') return stored !== null;
      // Text that is not a number counts as a change, so its check can speak.
      if (parsed.kind === 'invalid') return true;
      return parsed.value !== stored;
    }
    case 'date':
      return held === INCOMPLETE_DATE || held !== (stored ?? '');
    default:
      return held !== (stored === null ? '' : String(stored));
  }
}

/**
 * The value the save sends for a changed field (api-contract § 3.2): a text as
 * a string, any other in `{ value }`, empty as `{ value: null }`; `undefined`
 * when the held value cannot be sent — not a number, a date typed in part.
 */
function sentValue(
  name: WorkItemFieldName,
  held: string,
): FieldValue<string | number> | string | undefined {
  switch (kindOf(name)) {
    case 'text':
      return held;
    case 'number': {
      const parsed = parseNumber(held);
      if (parsed.kind === 'invalid') return undefined;
      return { value: parsed.kind === 'empty' ? null : parsed.value };
    }
    case 'date':
      return held === INCOMPLETE_DATE ? undefined : { value: held === '' ? null : held };
    case 'list': {
      if (held === '') return { value: null };
      const numeric = LIST_FIELDS[name as keyof typeof LIST_FIELDS].numeric;
      return { value: numeric ? Number(held) : held };
    }
  }
}

/** The type's fields whose held value differs from the stored one, in form order. */
export function changedFields(item: WorkItem, draft: FieldsDraft): WorkItemFieldName[] {
  return fieldsOf(item.type.appearance).filter((name) => differs(name, draft[name], item[name]));
}

/**
 * The save's members for the fields per type: exactly the type's fields that
 * changed (BR-M3-38) and can be sent. A field the type lacks is never sent
 * (BR-M3-30), whatever the draft holds.
 */
export function fieldChanges(item: WorkItem, draft: FieldsDraft): SaveFieldsBody {
  const body: Record<string, unknown> = {};
  for (const name of changedFields(item, draft)) {
    const value = sentValue(name, draft[name]);
    if (value !== undefined) {
      body[name] = value;
    }
  }
  return body as SaveFieldsBody;
}

const START: DateFieldName = 'startDate';
const TARGET: DateFieldName = 'targetDate';

/**
 * The browser's checks of the changed fields (spec § Controls and copy), each
 * under its field: a number's, a date's, a text's; then «Целевая дата» earlier
 * than «Дата начала» once either date changed and neither has a reason of its
 * own (BR-M3-35) — under «Целевая дата» also when only «Дата начала» changed.
 */
export function fieldErrors(
  item: WorkItem,
  draft: FieldsDraft,
): Partial<Record<WorkItemFieldName, string>> {
  const errors: Partial<Record<WorkItemFieldName, string>> = {};
  const changed = changedFields(item, draft);
  for (const name of changed) {
    const held = draft[name];
    const kind = kindOf(name);
    const error =
      kind === 'number'
        ? numberError(held, NUMBER_FIELDS[name as keyof typeof NUMBER_FIELDS].integer)
        : kind === 'date'
          ? dateError(held)
          : kind === 'text'
            ? textError(held)
            : null;
    if (error !== null) {
      errors[name] = error;
    }
  }
  const datesChanged = changed.includes(START) || changed.includes(TARGET);
  const start = draft[START];
  const target = draft[TARGET];
  if (
    datesChanged &&
    errors[START] === undefined &&
    errors[TARGET] === undefined &&
    start !== '' &&
    target !== '' &&
    target < start
  ) {
    errors[TARGET] = ruleTexts.targetBeforeStart;
  }
  return errors;
}
