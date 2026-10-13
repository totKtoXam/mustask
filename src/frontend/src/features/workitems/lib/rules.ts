// The browser's checks of «Заголовок» and «Описание» (spec § Dialog «Новый
// рабочий элемент», § Page: a work item's form; api-contract § 0.6), run on
// submit and cleared as the field is edited. The server runs the same rules
// and has the last word; these only spare a round trip. The texts are the
// spec's, the same as the catalog's.
import { checkDescription, normaliseRichText } from '../../../shared/lib/richText';

/** BR-M3-3: at most 255 UTF-16 code units after trimming. */
export const TITLE_MAX_LENGTH = 255;

export const ruleTexts = {
  titleRequired: 'Введите заголовок.',
  titleTooLong: 'Не более 255 символов.',
  titleMultiline: 'Заголовок должен быть в одну строку.',
  descriptionTooLong: 'Не более 100 000 символов.',
  // feat-018 api-contract § 4 / US-4: the reduced markup is over the ceiling.
  descriptionMarkupTooLarge:
    'Слишком много разметки. Упростите форматирование или сократите текст.',
  // feat-022 spec § Controls and copy: the checks of the fields per type.
  notANumber: 'Введите число.',
  numberOutOfRange: 'Допустимо значение от 0 до 9 999.',
  numberTooPrecise: 'Не более двух знаков после запятой.',
  integerRequired: 'Введите целое число.',
  dateIncomplete: 'Введите дату полностью.',
  dateOutOfRange: 'Укажите дату с 2000 по 2099 год.',
  targetBeforeStart: 'Целевая дата не может быть раньше даты начала.',
  textTooLong: 'Не более 100 000 символов.',
} as const;

/** The line breaks the server refuses in a title (api-contract § 0.6). */
const LINE_BREAK = /[\n\r\v\f\u0085\u2028\u2029]/;

/**
 * The first reason a title is refused, in the server's order — empty, too
 * long, multi-line — or `null`. Trimmed first, as the server trims it, so a
 * break at either end is white space, not a second line.
 */
export function titleError(title: string): string | null {
  const trimmed = title.trim();
  if (trimmed.length === 0) {
    return ruleTexts.titleRequired;
  }
  if (trimmed.length > TITLE_MAX_LENGTH) {
    return ruleTexts.titleTooLong;
  }
  if (LINE_BREAK.test(trimmed)) {
    return ruleTexts.titleMultiline;
  }
  return null;
}

/**
 * «Описание» is the editor's canonical html (feat-018): refused only for its
 * size, by the same function the editor's live counter uses (BR-RT-7, US-4) —
 * the markup ceiling first, so a value over both answers the markup text.
 */
export function descriptionError(html: string): string | null {
  switch (checkDescription(normaliseRichText(html))) {
    case 'markup-too-large':
      return ruleTexts.descriptionMarkupTooLarge;
    case 'too-long':
      return ruleTexts.descriptionTooLong;
    default:
      return null;
  }
}
