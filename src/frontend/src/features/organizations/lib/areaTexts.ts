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
export const ORGANIZATION_PLACE = 'Верхний уровень';

/** «Структура» (design § 5.2; the organization's tree). */
export const areasPage = {
  title: 'Структура',
  create: 'Создать департамент',
  search: 'Найти область',
  nothingFound: 'Ничего не найдено',
  resetFilter: 'Сбросить фильтр',
  expandAll: 'Развернуть всё',
  collapseAll: 'Свернуть всё',
  columns: { name: 'Название', type: 'Тип', description: 'Описание' },
  deactivated: 'деактивирован',
  rowMenu: (name: string) => `Действия: ${name}`,
  empty: {
    heading: 'В организации пока нет областей',
    adminHint: 'Создайте первый департамент: он объединяет проекты и команды.',
    otherHint: 'Департаменты создаёт администратор организации.',
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
  /** «Департамент: X», «Структура: A» for a project's node, «Область: X» (design § 6). */
  title: (type: AreaType, name: string) =>
    `${type === 'project' ? 'Структура' : typeLabels[type]}: ${name}`,
  topLevel: 'Верхний уровень',
  projectTeamsHint: 'Команды проекта — в разделе «Команды».',
  facts: {
    name: 'Название',
    type: 'Тип',
    location: 'Расположение',
    parent: 'Родительская область',
    project: 'Проект',
    projects: 'Проекты',
    teams: 'Команды',
    description: 'Описание',
  },
  nameFollowsProject: 'Название меняется вместе с проектом.',
  noDescription: 'Описание не задано',
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
    empty: 'В департаменте нет проектов',
    add: 'Добавить проект',
    create: 'Создать проект',
    liftToTop: 'Вынести на верхний уровень',
    deactivatedCount: (count: number) => `${count} деактивирован`,
  },
  teams: {
    title: 'Команды',
    add: 'Добавить команды',
    tableLabel: 'Команды департамента',
    columns: { team: 'Команда' },
    empty: 'К департаменту не привязаны команды',
    unlink: 'Отключить от департамента',
  },
  rowMenu: (name: string) => `Ещё действия: ${name}`,
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

/** «Добавление проекта в департамент» and «Вынести на верхний уровень» (feat-043 design §§ 8.2, 8.3). */
export const placeProjectDialog = {
  title: 'Добавление проекта в департамент',
  submit: 'Переместить',
  field: 'Проект',
  current: (name: string, place: string) => `${name} — сейчас: ${place}`,
  topLevelNow: 'верхний уровень',
  fromPlace: (project: string, from: string, to: string) =>
    `Проект «${project}» переместится из «${from}» в «${to}». Его области, команды и рабочие элементы не изменятся.`,
  fromTop: (project: string, to: string) =>
    `Проект «${project}» войдёт в департамент «${to}». Его области, команды и рабочие элементы не изменятся.`,
  moved: (project: string) => `Проект «${project}» перемещён.`,
  projectNotFound: 'Проект не найден',
} as const;

export const liftProjectDialog = {
  title: 'Вынести на верхний уровень',
  confirm: 'Вынести',
  text: (project: string) =>
    `Вынести проект «${project}» на верхний уровень? Команды, области и рабочие элементы проекта не изменятся.`,
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
    sectionName: '«Структура»',
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

/** Russian plural: `one` for 1, `few` for 2-4, `many` otherwise (11-14 are `many`). */
export function plural(n: number, one: string, few: string, many: string): string {
  const last = n % 10;
  const tail = n % 100;
  if (last === 1 && tail !== 11) return `${n} ${one}`;
  if (last >= 2 && last <= 4 && (tail < 12 || tail > 14)) return `${n} ${few}`;
  return `${n} ${many}`;
}

/** «Структура», the diagram view (feat-049 design § 7; final interface copy). */
export const structureDiagram = {
  viewLabel: 'Вид структуры',
  views: { tree: 'Дерево', diagram: 'Диаграмма' },
  tree: 'Диаграмма структуры',
  legend: {
    nesting: 'вложенность',
    relation: 'команда работает над проектом',
    copy: 'та же команда в другом департаменте',
    hidden: 'связь через скрытый узел',
  },
  sections: { outside: 'Без департамента', departments: 'Департаменты, проекты и области' },
  alsoIn: (names: string) => `ещё в: ${names}`,
  search: 'Найти узел',
  found: (n: number) => `Найдено: ${n}. Enter — к следующему`,
  notFound: 'Ничего не найдено',
  counters: (d: number, p: number, a: number, t: number | null) =>
    `Департаментов: ${d} · Проектов: ${p} · Областей: ${a}${t === null ? '' : ` · Команд: ${t}`}`,
  mode: {
    label: 'Показ при выборе',
    dim: { label: 'Затемнение', hint: 'Остальное приглушается' },
    only: {
      label: 'Только связанные',
      hint: 'Показаны ветка выбранного узла и связанные с ней',
    },
    hl: { label: 'Подсветка', hint: 'Всё на месте, ветка и связанное выделены цветом' },
  },
  layout: {
    label: 'Раскладка',
    h: { label: 'Горизонтально', hint: 'Иерархия слева направо' },
    v: { label: 'Вертикально', hint: 'Департаменты сверху' },
  },
  levelMode: {
    label: 'Режим уровней',
    only: { label: 'Только выбранные уровни', hint: 'Показаны отмеченные уровни' },
    from: {
      label: 'Уровень и вложенные',
      hint: 'Самый верхний из отмеченных и все более глубокие',
    },
  },
  tooltip: (group: string, value: string, hint: string) => `${group}: ${value}. ${hint}`,
  levels: {
    group: 'Уровни вложенности',
    chip: (n: number) => `Уровень ${n}`,
    all: 'Все уровни',
    allHint: 'Показать все уровни',
    shown: (levels: readonly number[], deeper: boolean) =>
      `Показаны уровни: ${levels.join(', ')}${deeper ? ' и более глубокие' : ''}`,
    shownAll: 'Показаны все уровни',
    none: 'Нет узлов на выбранных уровнях',
    noRelated: 'Нет связанных узлов',
    meaning: (n: number) =>
      n === 1
        ? 'Уровень 1 — департаменты, проекты верхнего уровня и команды без департамента.'
        : n === 2
          ? 'Уровень 2 — проекты и команды под департаментом, области под проектом верхнего уровня.'
          : `Уровень ${n} — области ${n}-го уровня вложенности.`,
    clickOnly: 'Нажмите, чтобы показать только этот уровень',
    clickFrom: 'Нажмите, чтобы показать этот и более глубокие уровни',
    clickRemove: 'Нажмите, чтобы убрать уровень из выбранных',
  },
  zoom: {
    group: 'Масштаб',
    out: 'Уменьшить масштаб (Ctrl + колесо мыши)',
    in: 'Увеличить масштаб (Ctrl + колесо мыши)',
    value: (percent: number) =>
      percent === 100 ? `Масштаб: ${percent}%` : `Масштаб: ${percent}%. Вернуть 100%`,
    valueTip: (percent: number) =>
      percent === 100
        ? `Масштаб: ${percent}%`
        : `Масштаб: ${percent}%. Нажмите, чтобы вернуть 100%`,
    announced: (percent: number) => `Масштаб: ${percent}%`,
  },
  counts: {
    teams: (n: number) => plural(n, 'команда', 'команды', 'команд'),
    projects: (n: number) => plural(n, 'проект', 'проекта', 'проектов'),
  },
  sectionAnnounced: (title: string, open: boolean) =>
    `${title}: ${open ? 'развёрнуто' : 'свёрнуто'}`,
  summary: {
    department: (projects: number, areas: number, teams: number, outside: string) =>
      `Ветка департамента: проектов ${projects}, областей ${areas}, команд ${teams}. Связано вне ветки: ${outside}`,
    project: (areas: number, teams: string) =>
      `Ветка проекта: областей ${areas}. Команды проекта: ${teams}`,
    team: (projects: string, departments: string) =>
      `Проекты команды: ${projects}. Департаменты команды: ${departments}`,
    none: 'нет',
    onlyRelated: 'Показаны только ветка выбранного узла и связанные с ней.',
  },
  /** The name of a chevron button; the card's own name is what a screen reader reads. */
  toggle: { open: 'Развернуть', close: 'Свернуть' },
  clear: 'Сбросить',
  cleared: 'Выбор сброшен',
  notes: {
    lines: 'Линии связей показаны для выбранного узла',
    offers: 'Команды и ключи проектов не загружены',
    retry: 'Повторить',
    vertical: (n: number) =>
      `Вертикальная раскладка широкая: показано узлов — ${n}. Сверните ветви, выберите уровни или переключитесь на горизонтальную.`,
  },
  toasts: {
    hiddenByMode: 'Узел скрыт режимом «Только связанные»',
    hiddenByLevels: 'Узел скрыт выбором уровней',
  },
  notChoosable: 'Этот узел выбрать нельзя',
  notOpening: 'Узел деактивированного проекта ничего не открывает',
  hint: 'Tab входит в диаграмму один раз. Стрелки — переход, Пробел — выбрать, Enter — открыть, Esc — сбросить. Ctrl + колесо мыши — масштаб, средняя кнопка мыши (зажать и двигать) — перемещение.',
  type: { department: 'Департамент', project: 'Проект', area: 'Область', team: 'Команда' },
  names: {
    department: (name: string, teams: number | null) =>
      `Департамент ${name}${teams === null ? '' : `, команд: ${teams}`}`,
    project: (
      name: string,
      key: string | null,
      active: boolean,
      department: string | null,
      teams: number | null,
    ) =>
      `Проект ${name}${key ? ` (${key})` : ''}${active ? '' : ', деактивирован'}, ${
        department ? `в департаменте ${department}` : 'без департамента'
      }${active && teams !== null ? `, команд: ${teams}` : ''}`,
    area: (name: string, parent: string, active: boolean) =>
      `Область ${name}, в «${parent}»${active ? '' : ', деактивирована'}`,
    team: (name: string, projects: number, department: string | null, also: string | null) =>
      `Команда ${name}, проектов: ${projects}${
        department ? `, в департаменте ${department}` : ', без департамента'
      }${also ? `, ${also}` : ''}`,
    section: (title: string, open: boolean, count: number) =>
      `Раздел «${title}», ${open ? 'развёрнут' : 'свёрнут'}, узлов: ${count}`,
  },
} as const;
