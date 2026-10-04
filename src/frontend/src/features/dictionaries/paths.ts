// The slice's addresses. The language registry is the landing page of
// «Настройки» → «Справочники» → «Языки» (mod-015 D-14: the address is kept);
// the form lives beneath it.
export const LANGUAGES_PATH = '/dictionaries/languages';
export const NEW_LANGUAGE_PATH = `${LANGUAGES_PATH}/new`;

export function languagePath(code: string): string {
  return `${LANGUAGES_PATH}/${encodeURIComponent(code)}`;
}
