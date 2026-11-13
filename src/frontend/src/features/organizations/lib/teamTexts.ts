// Every string of the organization's teams, their links and the descriptions that
// docs/features/feat-028-org-level-teams/design.md puts in the panel, in one place so no
// component types one (frontend.md "Forms"). The strings are the design's, final; the
// section in the comment is where the design says it. The server's refusal texts are the
// registry's and are shown as they come (`apiErrorText`), not repeated here.

/** «Команды» of the organization (design § 3). */
export const orgTeamsPage = {
  title: 'Команды',
  create: 'Новая команда',
  tableLabel: 'Команды организации',
  columns: { team: 'Команда', projects: 'Проекты', description: 'Описание' },
  noProjects: 'Нет проектов',
  rowMenu: (name: string) => `Ещё действия: ${name}`,
  empty: {
    heading: 'В организации пока нет команд',
    adminHint: 'Создайте команду кнопкой «Новая команда» и подключите её к проектам.',
    otherHint: 'Команды создаёт администратор организации.',
  },
} as const;

/** The commands a team has, on its row and on its page (design §§ 3, 4). */
export const teamCommands = {
  rename: 'Переименовать',
  describe: 'Изменить описание',
  delete: 'Удалить команду',
  move: 'Переместить',
} as const;

/** A team's page (design § 4). */
export const teamPage = {
  /** The title while the team loads, and when it fails. */
  titleLoading: 'Команда',
  title: (name: string) => `Команда: ${name}`,
  facts: {
    name: 'Название',
    department: 'Департамент',
    description: 'Описание',
    members: 'Участников',
    projects: 'Проектов',
  },
  noDescription: 'Описание не задано',
  projects: {
    title: 'Проекты',
    add: 'Добавить проекты',
    tableLabel: 'Проекты команды',
    columns: { project: 'Проект', key: 'Ключ', place: 'Расположение' },
    topLevel: '—',
    rowMenu: (name: string) => `Ещё действия: ${name}`,
    unlink: 'Отключить от проекта',
    empty: 'Команда не привязана ни к одному проекту',
  },
} as const;

/** «Новая команда» (design § 5.1). */
export const createTeamDialog = {
  title: 'Новая команда',
  submit: 'Создать',
  name: { label: 'Название', hint: 'От 1 до 100 символов.' },
  description: { label: 'Описание', hint: 'Необязательно. Не более 2000 символов.' },
  members: { label: 'Участники', hint: 'Среди активных участников организации.' },
  projects: {
    label: 'Проекты',
    hint: 'Необязательно. Можно добавить позже.',
    none: 'В организации нет активных проектов.',
  },
  /** doc-016 design § 4.1. */
  department: {
    label: 'Департамент',
    hint: 'Команда принадлежит одному департаменту.',
    required: 'Выберите департамент',
    none: 'Нет доступных департаментов',
    loadFailed: 'Не удалось загрузить список. Попробуйте ещё раз.',
  },
  created: (name: string) => `Команда «${name}» создана.`,
} as const;

/** «Перемещение команды» (doc-016 design § 4.2). */
export const moveTeamDialog = {
  title: (name: string) => `Перемещение команды «${name}»`,
  field: 'Новый департамент',
  required: 'Выберите департамент',
  hint: 'Участники, доступы и спринты остаются на месте.',
  submit: 'Переместить',
  noOther: 'Нет другого департамента, в который вы можете переместить команду.',
  projects: 'Проекты:',
  unlink: 'Отключить',
  unlinkRow: (project: string) => `Отключить от проекта «${project}»`,
  moved: (team: string, department: string) => `Команда «${team}» перемещена в «${department}».`,
} as const;

/** The rename (design § 5.2): today's `NameDialog` with its notice. */
export const renameTeamDialog = {
  title: 'Переименование команды',
  submit: 'Сохранить',
  renamed: 'Команда переименована.',
} as const;

/** «Описание команды», «Описание проекта» (design § 5.2): one dialog, the title and the notice are props. */
export const descriptionDialog = {
  team: { title: 'Описание команды', saved: 'Описание команды сохранено.' },
  project: { title: 'Описание проекта', saved: 'Описание проекта сохранено.' },
  submit: 'Сохранить',
  label: 'Описание',
  hint: 'Не более 2000 символов.',
} as const;

/** «Удаление команды» (design § 5.3). */
export const deleteTeamDialog = {
  title: 'Удаление команды',
  confirm: 'Удалить команду',
  /** The deletion with projects: the sentence before the per-project list. */
  withProjects: (name: string) =>
    `Удалить команду «${name}»? Рабочие элементы станут «Без команды»:`,
  /** mod-018 design § 7: the team's links to departments go with it. */
  areaLinks: 'Связи команды с департаментами тоже будут удалены.',
  irreversible: 'Восстановить команду будет нельзя.',
  /** The deletion of a team linked to none. */
  withoutProjects: (name: string) =>
    `Удалить команду «${name}»? Связи команды с департаментами тоже будут удалены. Восстановить команду будет нельзя.`,
  listLabel: 'Рабочие элементы по проектам',
  deactivated: 'деактивирован',
  deleted: (name: string) => `Команда «${name}» удалена.`,
} as const;

/** «Добавление команд в проект», «Добавление проектов в команду» (design § 6.1). */
export const linkDialog = {
  submit: 'Добавить',
  project: {
    title: 'Добавление команд в проект',
    label: 'Команды',
    text: (projectName: string) =>
      `Выбранные команды войдут в проект «${projectName}». Работу команд это не меняет.`,
    allLinked: 'Все команды уже подключены к проекту.',
    noneExist: 'В организации пока нет команд. Создайте первую в разделе «Организация» → ',
    sectionName: '«Команды»',
    loadFailed: 'Не удалось загрузить команды. Попробуйте ещё раз.',
    linkedOne: (teamName: string) => `Команда «${teamName}» добавлена.`,
    linkedMany: (count: number) => `Команды добавлены: ${count}.`,
  },
  team: {
    title: 'Добавление проектов в команду',
    label: 'Проекты',
    text: (teamName: string) =>
      `Команда «${teamName}» войдёт в выбранные проекты. Её работу это не меняет.`,
    allLinked: 'Команда уже подключена ко всем проектам.',
    noneExist: 'В организации нет активных проектов.',
    loadFailed: 'Не удалось загрузить проекты. Попробуйте ещё раз.',
    linkedOne: (projectName: string) => `Проект «${projectName}» добавлен.`,
    linkedMany: (count: number) => `Проекты добавлены: ${count}.`,
  },
  retry: 'Повторить',
} as const;

/** «Отключение команды от проекта» (design § 6.2). */
export const unlinkDialog = {
  title: 'Отключение команды от проекта',
  confirm: 'Отключить',
  owns: (teamName: string, projectName: string, count: number) =>
    `Команда «${teamName}» владеет рабочими элементами проекта «${projectName}»: ${count}. Выберите, кому их передать.`,
  field: 'Передать рабочие элементы',
  fieldPlaceholder: 'Выберите команду или «Без команды»',
  noTeam: 'Без команды',
  toTeam: (teamName: string) => `Рабочие элементы встанут в конец бэклогов команды «${teamName}».`,
  toNobody:
    'Рабочие элементы останутся в бэклоге и на доске проекта, но не попадут в бэклог ни одной команды.',
  caption: (teamName: string) =>
    `Рабочие элементы не удаляются. Если «${teamName}» — команда проекта по умолчанию, настройка сбросится. Повторное подключение команды рабочие элементы не вернёт.`,
  ownsNone: (teamName: string, projectName: string) =>
    `Отключить команду «${teamName}» от проекта «${projectName}»?`,
  captionNone: (teamName: string) =>
    `Если «${teamName}» — команда проекта по умолчанию, настройка сбросится.`,
  unlinked: 'Команда отключена от проекта.',
} as const;

/** The project's «Команды» (design § 7). */
export const projectTeamsPage = {
  title: 'Команды',
  column: 'Команда',
  tableLabel: 'Команды проекта',
  rowMenu: (name: string) => `Ещё действия: ${name}`,
  add: 'Добавить команды',
  unlink: 'Отключить от проекта',
  empty: {
    heading: 'К проекту не привязана ни одна команда',
    adminHint:
      'Добавьте команду организации — она сможет владеть рабочими элементами проекта. Без команд проект тоже работает: у его рабочих элементов будет «Без команды».',
    otherHint: 'Команды подключает администратор организации. Проект работает и без команд.',
  },
} as const;

/** The project's description and its creation form (design § 8). */
export const projectTexts = {
  mainSection: 'Основное',
  placeSection: 'Расположение',
  noDepartment: 'Проект не входит ни в один департамент',
  keyFixed: 'Ключ нельзя изменить.',
  defaultTeamSection: 'Команда по умолчанию',
  noDefaultTeam: 'Не выбрана',
  chooseDefaultTeam: 'Выбрать',
  describe: 'Изменить описание',
  factDescription: 'Описание',
  noDescription: 'Описание не задано',
  create: {
    nameHint: 'От 1 до 100 символов. Так же будет называться корневая область проекта.',
    description: { label: 'Описание', hint: 'Необязательно. Не более 2000 символов.' },
    teams: {
      label: 'Команды',
      hint: 'Необязательно. Команды можно добавить позже на странице «Команды» проекта.',
      none: 'В организации пока нет команд.',
    },
  },
  rename: {
    text: 'Корневая область переименуется вместе с проектом. Ключ, описание и команды проекта не изменятся.',
  },
  deactivate: {
    text: (name: string, key: string) =>
      `Проект «${name}» (${key}) пропадёт из списков и селекторов, а его страницы перестанут открываться. Ничего не будет удалено, связи с командами сохранятся: проект можно восстановить в списке деактивированных проектов организации. Область проекта сохранится, но не будет открываться, пока проект деактивирован.`,
  },
} as const;

/** The description's own error, checked on the browser's side before a send (design § 5.1, § 5.2). */
export const DESCRIPTION_LENGTH_TEXT = 'Описание должно содержать не более 2000 символов';

/** BR-OL-3: a description is at most 2 000 characters, kept as typed. */
export const DESCRIPTION_MAX_LENGTH = 2000;
