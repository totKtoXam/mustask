// Every string of the organization's area workspace that docs/features/mod-018-area-workspace/design.md
// puts in the panel, in one place so no component types one (frontend.md "Forms"). The strings are the
// design's, final; the section in the comment is where the design says it. The server's refusal texts are
// the registry's and are shown as they come (`apiErrorText`), not repeated here.

/** The tree's initial expansion (design D-3): everything while the tree has at most this many areas. */
export const TREE_EXPAND_ALL_LIMIT = 100;

/** The pickers' list draws at most this many matches (design D-1). */
export const PICKER_OPTION_LIMIT = 50;

/** «Области» (design § 3). */
export const areasPage = {
  title: 'Области',
  create: 'Новая область',
  expandAll: 'Развернуть всё',
  collapseAll: 'Свернуть всё',
  columns: { area: 'Область', description: 'Описание' },
  rowMenu: (name: string) => `Действия: ${name}`,
  empty: {
    heading: 'В организации пока нет областей',
    adminHint:
      'Область объединяет проекты и команды организации. Создайте первую кнопкой «Новая область».',
    otherHint: 'Области создаёт администратор организации.',
  },
} as const;

/** The commands an area has, on its row and on its page (design §§ 3, 4). */
export const areaCommands = {
  createChild: 'Новая дочерняя область',
  rename: 'Переименовать',
  describe: 'Изменить описание',
  move: 'Переместить',
  delete: 'Удалить область',
} as const;

/** An area's page (design § 4). */
export const areaPage = {
  /** The title while the area loads, and when it fails. */
  titleLoading: 'Область',
  title: (name: string) => `Область: ${name}`,
  facts: { name: 'Название', parent: 'Родительская область', description: 'Описание' },
  topLevel: 'Верхний уровень',
  noDescription: 'Описание не задано',
  children: {
    title: 'Вложенные области',
    tableLabel: 'Вложенные области',
    columns: { area: 'Область', description: 'Описание' },
    empty: 'Вложенных областей пока нет.',
  },
  projects: {
    title: 'Проекты',
    add: 'Добавить проект',
    tableLabel: 'Проекты области',
    columns: { project: 'Проект', key: 'Ключ' },
    empty: 'К области не привязаны проекты',
    unlink: 'Отключить от области',
  },
  teams: {
    title: 'Команды',
    add: 'Добавить команду',
    tableLabel: 'Команды области',
    columns: { team: 'Команда' },
    empty: 'К области не привязаны команды',
    unlink: 'Отключить от области',
  },
  rowMenu: (name: string) => `Ещё действия: ${name}`,
} as const;

/** The section «Области» of a project's «Общие» and of a team's page (design § 7). */
export const endAreasSection = {
  title: 'Области',
  add: 'Добавить область',
  project: {
    tableLabel: 'Области проекта',
    empty: 'Проект не входит ни в одну область',
    unlink: 'Отключить от проекта',
  },
  team: {
    tableLabel: 'Области команды',
    empty: 'Команда не входит ни в одну область',
    unlink: 'Отключить от команды',
  },
  column: 'Область',
  rowMenu: (name: string) => `Ещё действия: ${name}`,
} as const;

/** «Новая область» (design § 5.1). */
export const createAreaDialog = {
  title: 'Новая область',
  submit: 'Создать',
  name: { label: 'Название', hint: 'От 1 до 100 символов.' },
  parent: {
    label: 'Родительская область',
    hint: 'Необязательно. По умолчанию область создаётся на верхнем уровне.',
  },
  description: { label: 'Описание', hint: 'Необязательно. Не более 2000 символов.' },
  projects: {
    label: 'Проекты',
    hint: 'Необязательно. Можно добавить позже.',
    none: 'В организации нет активных проектов.',
  },
  teams: {
    label: 'Команды',
    hint: 'Необязательно. Можно добавить позже.',
    none: 'В организации пока нет команд.',
  },
  created: (name: string) => `Область «${name}» создана.`,
} as const;

/** The rename and the description (design § 5.2). */
export const areaEditDialogs = {
  rename: {
    title: 'Переименование области',
    submit: 'Сохранить',
    helper: 'От 1 до 100 символов',
    renamed: 'Область переименована.',
  },
  description: { title: 'Описание области', saved: 'Описание области сохранено.' },
} as const;

/** «Перемещение области» (design § 5.3). */
export const moveAreaDialog = {
  title: 'Перемещение области',
  submit: 'Переместить',
  field: 'Новая родительская область',
  withChildren: (name: string, count: number) =>
    `Область «${name}» переместится вместе со всеми вложенными областями: ${count}.`,
  leaf: (name: string) => `Область «${name}» переместится.`,
  moved: (name: string) => `Область «${name}» перемещена.`,
} as const;

/** «Удаление области» (design § 5.4). */
export const deleteAreaDialog = {
  title: 'Удаление области',
  confirm: 'Удалить область',
  withLinks: (name: string) =>
    `Удалить область «${name}»? Её связи будут удалены, сами проекты и команды не изменятся.`,
  noLinks: (name: string) => `Удалить область «${name}»? Восстановить область будет нельзя.`,
  irreversible: 'Восстановить область будет нельзя.',
  hasChildren: 'Нельзя удалить область, в которой есть вложенные области',
  teamsList: (count: number) => `Команды (${count})`,
  projectsList: (count: number) => `Проекты (${count})`,
  deactivated: 'деактивирован',
  notFound: 'Область не найдена.',
  forbidden: 'Недостаточно прав',
  deleted: (name: string) => `Область «${name}» удалена.`,
} as const;

/** The four ends of the link dialog (design § 6.1). */
export type AreaLinkEnd = 'areaProjects' | 'areaTeams' | 'projectAreas' | 'teamAreas';

export const areaLinkDialog = {
  submit: 'Добавить',
  retry: 'Повторить',
  more: (shown: number, total: number) => `Показаны первые ${shown} из ${total}. Уточните запрос.`,
  areaProjects: {
    title: 'Добавление проектов в область',
    label: 'Проекты',
    text: (own: string) =>
      `Выбранные проекты войдут в область «${own}». Их рабочие элементы и доступ к ним это не меняет.`,
    allLinked: 'Все проекты уже входят в область.',
    noneExist: 'В организации нет активных проектов.',
    loadFailed: 'Не удалось загрузить проекты. Попробуйте ещё раз.',
    notFound: 'Проект не найден.',
    linkedOne: (name: string) => `Проект «${name}» добавлен.`,
    linkedMany: (count: number) => `Проекты добавлены: ${count}.`,
  },
  areaTeams: {
    title: 'Добавление команд в область',
    label: 'Команды',
    text: (own: string) =>
      `Выбранные команды войдут в область «${own}». Работу команд и доступ их участников это не меняет.`,
    allLinked: 'Все команды уже входят в область.',
    noneExist: 'В организации пока нет команд. Создайте первую в разделе «Организация» → ',
    sectionName: '«Команды»',
    loadFailed: 'Не удалось загрузить команды. Попробуйте ещё раз.',
    notFound: 'Команда не найдена.',
    linkedOne: (name: string) => `Команда «${name}» добавлена.`,
    linkedMany: (count: number) => `Команды добавлены: ${count}.`,
  },
  projectAreas: {
    title: 'Добавление областей в проект',
    label: 'Области',
    text: (own: string) =>
      `Проект «${own}» войдёт в выбранные области. Его рабочие элементы и доступ к ним это не меняет.`,
    allLinked: 'Проект уже входит во все области.',
    noneExist: 'В организации пока нет областей. Создайте первую в разделе «Организация» → ',
    sectionName: '«Области»',
    loadFailed: 'Не удалось загрузить области. Попробуйте ещё раз.',
    notFound: 'Область не найдена.',
    linkedOne: (name: string) => `Область «${name}» добавлена.`,
    linkedMany: (count: number) => `Области добавлены: ${count}.`,
  },
  teamAreas: {
    title: 'Добавление областей в команду',
    label: 'Области',
    text: (own: string) =>
      `Команда «${own}» войдёт в выбранные области. Её работу и доступ её участников это не меняет.`,
    allLinked: 'Команда уже входит во все области.',
    noneExist: 'В организации пока нет областей. Создайте первую в разделе «Организация» → ',
    sectionName: '«Области»',
    loadFailed: 'Не удалось загрузить области. Попробуйте ещё раз.',
    notFound: 'Область не найдена.',
    linkedOne: (name: string) => `Область «${name}» добавлена.`,
    linkedMany: (count: number) => `Области добавлены: ${count}.`,
  },
} as const;

/** «Отключение …» (design § 6.2). */
export const areaUnlinkDialog = {
  confirm: 'Отключить',
  areaProject: {
    title: 'Отключение проекта от области',
    text: (name: string, own: string) =>
      `Отключить проект «${name}» от области «${own}»? Сам проект и его рабочие элементы не изменятся.`,
    done: 'Проект отключён от области.',
  },
  areaTeam: {
    title: 'Отключение команды от области',
    text: (name: string, own: string) =>
      `Отключить команду «${name}» от области «${own}»? Сама команда и её рабочие элементы не изменятся.`,
    done: 'Команда отключена от области.',
  },
  projectArea: {
    title: 'Отключение области от проекта',
    text: (name: string, own: string) =>
      `Отключить область «${name}» от проекта «${own}»? Сама область и проект не изменятся.`,
    done: 'Область отключена от проекта.',
  },
  teamArea: {
    title: 'Отключение области от команды',
    text: (name: string, own: string) =>
      `Отключить область «${name}» от команды «${own}»? Сама область и команда не изменятся.`,
    done: 'Область отключена от команды.',
  },
} as const;

/** The pickers of «Новый проект» and «Новая команда» (design § 7). */
export const areasField = {
  label: 'Области',
  none: 'В организации пока нет областей.',
  projectHint: 'Необязательно. Области можно добавить позже на странице проекта.',
  teamHint: 'Необязательно. Области можно добавить позже на странице команды.',
} as const;
