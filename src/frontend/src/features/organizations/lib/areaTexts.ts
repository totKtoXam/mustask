// Every string of the organization's typed area tree that docs/features/mod-018-area-workspace/design.md
// puts in the panel, in one place so no component types one (frontend.md "Forms"). The strings are the
// design's, final; the section in the comment is where the design says it. The server's refusal texts are
// the registry's and are shown as they come (`apiErrorText`), not repeated here.
import type { AreaType } from '../api/organizationsApi';

/** The tree's initial expansion (design D-3): everything while the tree has at most this many nodes. */
export const TREE_EXPAND_ALL_LIMIT = 100;

/** The pickers' list draws at most this many matches (design D-1). */
export const PICKER_OPTION_LIMIT = 50;

/** The type marks (design § 2): the label is the text, the icon is decoration. */
export const typeLabels: Readonly<Record<AreaType, string>> = {
  department: 'Департамент',
  project: 'Проект',
  area: 'Область',
};

/** «Организация»: the top level as a place, never a node (design D-8). */
export const ORGANIZATION_PLACE = 'Организация';

/** «Области» (design § 3). */
export const areasPage = {
  title: 'Области',
  create: 'Новый департамент',
  expandAll: 'Развернуть всё',
  collapseAll: 'Свернуть всё',
  columns: { name: 'Название', type: 'Тип', description: 'Описание' },
  deactivated: 'деактивирован',
  rowMenu: (name: string) => `Действия: ${name}`,
  empty: {
    heading: 'В организации пока нет департаментов и проектов',
    adminHint:
      'Департамент объединяет проекты и команды. Создайте первый кнопкой «Новый департамент»; проекты появляются в разделе «Проекты».',
    otherHint: 'Области создаёт администратор организации.',
  },
} as const;

/** The commands a node has, on its row and on its page (design §§ 3, 4). */
export const areaCommands = {
  createChild: 'Новая вложенная область',
  rename: 'Переименовать',
  describe: 'Изменить описание',
  move: 'Переместить',
  deleteDepartment: 'Удалить департамент',
  deleteArea: 'Удалить область',
  openProject: 'Открыть проект',
} as const;

/** A node's page (design § 4). */
export const areaPage = {
  /** The title while the node loads, and when it fails. */
  titleLoading: 'Область',
  title: (type: AreaType, name: string) => `${typeLabels[type]}: ${name}`,
  facts: {
    name: 'Название',
    type: 'Тип',
    location: 'Расположение',
    parent: 'Родительская область',
    project: 'Проект',
    description: 'Описание',
  },
  nameFollowsProject: 'Название меняется вместе с проектом.',
  noDescription: 'Описание не задано',
  deactivatedNotice: (projectName: string) =>
    `Проект «${projectName}» деактивирован: область доступна только для чтения. Чтобы изменить её, восстановите проект в списке деактивированных проектов организации.`,
  children: {
    title: 'Вложенные области',
    tableLabel: 'Вложенные области',
    columns: { area: 'Область', description: 'Описание' },
    empty: 'Вложенных областей пока нет.',
  },
  projects: {
    title: 'Проекты',
    tableLabel: 'Проекты департамента',
    columns: { project: 'Проект', key: 'Ключ' },
    empty: 'В департаменте пока нет проектов.',
  },
  teams: {
    title: 'Команды',
    add: 'Добавить команду',
    tableLabel: 'Команды департамента',
    columns: { team: 'Команда' },
    empty: 'К области не привязаны команды',
    unlink: 'Отключить от департамента',
  },
  projectTeams: {
    title: 'Команды',
    sameLinks: 'Это те же связи, что в разделе «Команды» проекта.',
    tableLabel: 'Команды проекта',
    add: 'Добавить команду',
    unlink: 'Отключить от проекта',
  },
  rowMenu: (name: string) => `Ещё действия: ${name}`,
} as const;

/** The project's «Области» page (design § 7). */
export const projectAreasPage = {
  title: 'Области',
  emptyChildren:
    'У проекта пока нет вложенных областей. Создайте первую кнопкой «Новая вложенная область».',
  emptyChildrenReader: 'У проекта нет вложенных областей.',
} as const;

/** The section «Департаменты» of a team's page (design § 6, D-10). */
export const teamDepartmentsSection = {
  title: 'Департаменты',
  add: 'Добавить департамент',
  tableLabel: 'Департаменты команды',
  column: 'Департамент',
  empty: 'Команда не входит ни в один департамент',
  unlink: 'Отключить от команды',
  rowMenu: (name: string) => `Ещё действия: ${name}`,
} as const;

/** «Новый департамент» (design § 5.1). */
export const createDepartmentDialog = {
  title: 'Новый департамент',
  submit: 'Создать',
  name: { label: 'Название', hint: 'От 1 до 100 символов.' },
  description: { label: 'Описание', hint: 'Необязательно. Не более 2000 символов.' },
  teams: {
    label: 'Команды',
    hint: 'Необязательно. Можно добавить позже.',
    none: 'В организации пока нет команд.',
  },
  created: (name: string) => `Департамент «${name}» создан.`,
} as const;

/** «Новая вложенная область» (design § 5.2). */
export const createSubAreaDialog = {
  title: 'Новая вложенная область',
  submit: 'Создать',
  name: { label: 'Название', hint: 'От 1 до 100 символов.' },
  parent: {
    label: 'Родительская область',
    helper: (projectName: string) => `Область создаётся внутри проекта «${projectName}».`,
  },
  description: { label: 'Описание', hint: 'Необязательно. Не более 2000 символов.' },
  created: (name: string) => `Область «${name}» создана.`,
} as const;

/** The rename and the description (design § 5.3). */
export const areaEditDialogs = {
  rename: {
    areaTitle: 'Переименование области',
    departmentTitle: 'Переименование департамента',
    submit: 'Сохранить',
    helper: 'От 1 до 100 символов',
    areaRenamed: 'Область переименована.',
    departmentRenamed: 'Департамент переименован.',
  },
  description: { title: 'Описание области', saved: 'Описание сохранено.' },
} as const;

/** The move, per type (design § 5.4). */
export const moveAreaDialog = {
  submit: 'Переместить',
  project: {
    title: 'Перемещение проекта',
    field: 'Новое расположение',
    withChildren: (name: string, count: number) =>
      `Проект «${name}» переместится вместе со всеми вложенными областями: ${count}. Рабочие элементы, команды и связи проекта не изменятся.`,
    leaf: (name: string) =>
      `Проект «${name}» переместится. Рабочие элементы, команды и связи проекта не изменятся.`,
    moved: (name: string) => `Проект «${name}» перемещён.`,
  },
  area: {
    title: 'Перемещение области',
    field: 'Новая родительская область',
    withChildren: (name: string, count: number) =>
      `Область «${name}» переместится вместе со всеми вложенными областями: ${count}.`,
    leaf: (name: string) => `Область «${name}» переместится.`,
    moved: (name: string) => `Область «${name}» перемещена.`,
  },
} as const;

/** The deletion (design § 5.5). */
export const deleteAreaDialog = {
  departmentTitle: 'Удаление департамента',
  areaTitle: 'Удаление области',
  departmentConfirm: 'Удалить департамент',
  areaConfirm: 'Удалить область',
  withTeams: (name: string) =>
    `Удалить департамент «${name}»? Его связи с командами будут удалены, сами команды не изменятся.`,
  departmentNoTeams: (name: string) =>
    `Удалить департамент «${name}»? Восстановить департамент будет нельзя.`,
  departmentIrreversible: 'Восстановить департамент будет нельзя.',
  area: (name: string) => `Удалить область «${name}»? Восстановить область будет нельзя.`,
  hasChildren:
    'Нельзя удалить область, в которой есть проекты, вложенные области или рабочие элементы',
  teamsList: (count: number) => `Команды (${count})`,
  notFound: 'Область не найдена.',
  forbidden: 'Недостаточно прав',
  departmentDeleted: (name: string) => `Департамент «${name}» удалён.`,
  areaDeleted: (name: string) => `Область «${name}» удалена.`,
} as const;

/** The two ends of the link dialog (design § 6.1). */
export type AreaLinkEnd = 'departmentTeams' | 'teamDepartments';

export const areaLinkDialog = {
  submit: 'Добавить',
  retry: 'Повторить',
  more: (shown: number, total: number) => `Показаны первые ${shown} из ${total}. Уточните запрос.`,
  departmentTeams: {
    title: 'Добавление команд в департамент',
    label: 'Команды',
    text: (own: string) =>
      `Выбранные команды войдут в департамент «${own}». Работу команд и доступ их участников это не меняет.`,
    allLinked: 'Все команды уже входят в департамент.',
    noneExist: 'В организации пока нет команд. Создайте первую в разделе «Организация» → ',
    sectionName: '«Команды»',
    loadFailed: 'Не удалось загрузить команды. Попробуйте ещё раз.',
    linkedOne: (name: string) => `Команда «${name}» добавлена.`,
    linkedMany: (count: number) => `Команды добавлены: ${count}.`,
  },
  teamDepartments: {
    title: 'Добавление департаментов в команду',
    label: 'Департаменты',
    text: (own: string) =>
      `Команда «${own}» войдёт в выбранные департаменты. Её работу и доступ её участников это не меняет.`,
    allLinked: 'Команда уже входит во все департаменты.',
    noneExist: 'В организации пока нет департаментов. Создайте первый в разделе «Организация» → ',
    sectionName: '«Области»',
    loadFailed: 'Не удалось загрузить департаменты. Попробуйте ещё раз.',
    linkedOne: (name: string) => `Департамент «${name}» добавлен.`,
    linkedMany: (count: number) => `Департаменты добавлены: ${count}.`,
  },
} as const;

/** «Отключение …» (design § 6.2). */
export const areaUnlinkDialog = {
  confirm: 'Отключить',
  departmentTeam: {
    title: 'Отключение команды от департамента',
    text: (name: string, own: string) =>
      `Отключить команду «${name}» от департамента «${own}»? Сама команда и её рабочие элементы не изменятся.`,
    done: 'Команда отключена от департамента.',
  },
  teamDepartment: {
    title: 'Отключение департамента от команды',
    text: (name: string, own: string) =>
      `Отключить департамент «${name}» от команды «${own}»? Сам департамент и команда не изменятся.`,
    done: 'Департамент отключён от команды.',
  },
} as const;

/** «Расположение» of «Новый проект» and of the project's «Общие» (design § 7). */
export const placementField = {
  label: 'Расположение',
  hint: 'Необязательно. Без департамента проект появится на верхнем уровне.',
  move: 'Переместить',
} as const;

/** The department picker of «Новая команда». */
export const departmentsField = {
  label: 'Департаменты',
  none: 'В организации пока нет департаментов.',
  teamHint: 'Необязательно. Департаменты можно добавить позже на странице команды.',
} as const;
