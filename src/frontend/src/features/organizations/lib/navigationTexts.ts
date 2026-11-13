// The strings of feat-043's pages (design §§ 3-8), final, in one place so no component types one
// (frontend.md "Forms"). The section in the comment is where the design says it.

/** «Обзор» (design § 5.1). */
export const overviewPage = {
  title: 'Обзор',
  myTeams: 'Мои команды',
  statLabel: 'Количество',
  departments: 'Департаменты',
  projects: 'Проекты',
  teams: 'Команды',
  allDepartments: (count: number) => `Все департаменты (${count})`,
  allProjects: (count: number) => `Все проекты (${count})`,
  allTeams: (count: number) => `Все команды (${count})`,
  noDepartments: 'Департаментов пока нет',
  noProjects: 'Проектов пока нет',
  noTeams: 'Команд пока нет',
  organizations: 'Организации',
  currentOrganization: 'текущая',
  allOrganizations: 'Все организации',
} as const;

/** «Проекты» of the organization (design § 5.3). */
export const projectsList = {
  title: 'Проекты',
  create: 'Создать проект',
  search: 'Найти проект',
  show: 'Показывать',
  active: 'Активные',
  deactivated: 'Деактивированные',
  tableLabel: 'Проекты',
  columns: { name: 'Название', key: 'Ключ', place: 'Расположение' },
  topLevel: '—',
  rowMenu: (name: string) => `Ещё действия: ${name}`,
  deactivate: 'Деактивировать',
  restore: 'Восстановить',
  empty: {
    heading: 'Проектов пока нет',
    adminHint: 'Создайте первый проект.',
    otherHint: 'Проекты создаёт администратор организации.',
  },
  emptyDeactivated: 'Деактивированных проектов нет',
  nothingFound: 'Ничего не найдено',
  resetFilter: 'Сбросить фильтр',
} as const;

/** «Команды» of the organization (design § 5.3). */
export const teamsList = {
  title: 'Команды',
  create: 'Создать команду',
  search: 'Найти команду',
  tableLabel: 'Команды',
  columns: { name: 'Название', department: 'Департамент', description: 'Описание' },
  mine: 'Моя команда',
  rowMenu: (name: string) => `Ещё действия: ${name}`,
  empty: {
    heading: 'Команд пока нет',
    adminHint: 'Создайте первую команду.',
    otherHint: 'Команды создаёт администратор организации.',
  },
  nothingFound: 'Ничего не найдено',
  resetFilter: 'Сбросить фильтр',
} as const;
