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
} as const;

/** A team's page (design § 4). */
export const teamPage = {
  /** The title while the team loads, and when it fails. */
  titleLoading: 'Команда',
  title: (name: string) => `Команда: ${name}`,
  facts: { name: 'Название', description: 'Описание' },
  noDescription: 'Описание не задано',
  projects: {
    title: 'Проекты',
    add: 'Добавить проект',
    tableLabel: 'Проекты команды',
    columns: { project: 'Проект', key: 'Ключ' },
    rowMenu: (name: string) => `Ещё действия: ${name}`,
    unlink: 'Отключить от проекта',
    empty: 'Команда пока не участвует ни в одном проекте.',
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
  created: (name: string) => `Команда «${name}» создана.`,
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
  /** BR-AW-10: the team's links to areas go with it. */
  areaLinks: 'Связи команды с областями тоже будут удалены.',
  irreversible: 'Восстановить команду будет нельзя.',
  /** The deletion of a team linked to none. */
  withoutProjects: (name: string) =>
    `Удалить команду «${name}»? Связи команды с областями тоже будут удалены. Восстановить команду будет нельзя.`,
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
      `Выбранные команды смогут владеть рабочими элементами проекта «${projectName}». Доступ их участников к проекту это не меняет.`,
    allLinked: 'Все команды организации уже работают над проектом.',
    noneExist: 'В организации пока нет команд. Создайте первую в разделе «Организация» → ',
    sectionName: '«Команды»',
    loadFailed: 'Не удалось загрузить команды. Попробуйте ещё раз.',
    linkedOne: (teamName: string) => `Команда «${teamName}» добавлена в проект.`,
    linkedMany: (count: number) => `Команды добавлены в проект: ${count}.`,
  },
  team: {
    title: 'Добавление проектов в команду',
    label: 'Проекты',
    text: (teamName: string) =>
      `Команда «${teamName}» сможет владеть рабочими элементами выбранных проектов. Доступ её участников к ним это не меняет.`,
    allLinked: 'Команда уже работает над всеми проектами организации.',
    noneExist: 'В организации нет активных проектов.',
    loadFailed: 'Не удалось загрузить проекты. Попробуйте ещё раз.',
    linkedOne: (projectName: string) => `Проект «${projectName}» добавлен в команду.`,
    linkedMany: (count: number) => `Проекты добавлены в команду: ${count}.`,
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
  intro:
    'Команды организации, работающие над проектом. Создать, переименовать или удалить команду можно в разделе «Организация» → ',
  introSection: '«Команды»',
  add: 'Добавить команду',
  defaultTeam: 'Команда по умолчанию',
  projectBacklog: 'Бэклог проекта',
  projectBoard: 'Доска проекта',
  unlink: 'Отключить от проекта',
  empty: {
    heading: 'В проекте пока нет команд.',
    adminHint:
      'Добавьте команду организации — она сможет владеть рабочими элементами проекта. Без команд проект тоже работает: у его рабочих элементов будет «Без команды».',
    otherHint: 'Команды подключает администратор организации. Проект работает и без команд.',
  },
} as const;

/** The project's description and its creation form (design § 8). */
export const projectTexts = {
  describe: 'Изменить описание',
  factDescription: 'Описание',
  noDescription: 'Описание не задано',
  create: {
    nameHint: 'От 1 до 100 символов.',
    description: { label: 'Описание', hint: 'Необязательно. Не более 2000 символов.' },
    teams: {
      label: 'Команды',
      hint: 'Необязательно. Команды можно добавить позже на странице «Команды» проекта.',
      none: 'В организации пока нет команд.',
    },
  },
  rename: {
    text: 'Ключ, описание, команды и области проекта не изменятся.',
  },
  deactivate: {
    text: (name: string, key: string) =>
      `Проект «${name}» (${key}) пропадёт из списков и переключателя, а его страницы перестанут открываться. Ничего не будет удалено, связи с командами и областями сохранятся: проект можно восстановить в списке деактивированных проектов организации.`,
  },
} as const;

/** The description's own error, checked on the browser's side before a send (design § 5.1, § 5.2). */
export const DESCRIPTION_LENGTH_TEXT = 'Описание должно содержать не более 2000 символов';

/** BR-OL-3: a description is at most 2 000 characters, kept as typed. */
export const DESCRIPTION_MAX_LENGTH = 2000;
