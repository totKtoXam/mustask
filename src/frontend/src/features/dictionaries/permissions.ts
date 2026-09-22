// Коды прав этого среза: каждый экран/действие проверяет ровно свой код через
// `useAuth().can(code)`, никогда — имя роли. Названный объект, а не
// рассеянные литералы: точки шлюзования (маршруты и видимость кнопок) читают
// эти коды из нескольких мест — ровно случай "закрытого набора", который
// frontend.md просит именовать.
export const dictionariesPermissions = {
  languagesRead: 'dictionaries.languages.read',
  languagesWrite: 'dictionaries.languages.write',
} as const;

export type DictionariesPermission = (typeof dictionariesPermissions)[keyof typeof dictionariesPermissions];
