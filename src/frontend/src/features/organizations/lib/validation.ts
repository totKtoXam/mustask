// The three checks the browser makes before it sends (design § 4.4): only
// what the stories state, checked on submit. A refused field sends nothing.
// The texts are design § 4.4's, the same as the server's catalog.

export const NAME_LENGTH_TEXT = 'Название должно содержать от 1 до 100 символов';
export const KEY_REQUIRED_TEXT = 'Укажите ключ проекта';
export const KEY_FORMAT_TEXT =
  'Ключ проекта: от 2 до 10 латинских заглавных букв и цифр, начиная с буквы';

/** BR-M1-1: a name is 1–100 characters once trimmed. */
export const NAME_MAX_LENGTH = 100;

/** api-contract § 2.3: the key as sent, not trimmed and not upper-cased. */
export const PROJECT_KEY_PATTERN = /^[A-Z][A-Z0-9]{1,9}$/;

/** The name's refusal, or `null`: the trimmed length is 1–100. */
export function nameError(value: string): string | null {
  const length = value.trim().length;
  return length >= 1 && length <= NAME_MAX_LENGTH ? null : NAME_LENGTH_TEXT;
}

/** The key's refusal, or `null`. The key is checked as typed (US-3: `prm` is refused). */
export function keyError(value: string): string | null {
  if (value === '') {
    return KEY_REQUIRED_TEXT;
  }
  return PROJECT_KEY_PATTERN.test(value) ? null : KEY_FORMAT_TEXT;
}
