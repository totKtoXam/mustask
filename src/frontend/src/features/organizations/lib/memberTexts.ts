// Every string of «Участники» and of the team members that the spec and the
// design (mod-016 design § 3, § 4, § 13) put in the panel, in one place so no
// component types one (frontend.md "Forms"). The strings the spec quotes are
// verbatim; the ones the design adds are marked (added) in design § 13.

/** The page and its table. */
export const membersPage = {
  title: 'Участники',
  add: 'Добавить участника',
  tableLabel: 'Участники организации',
  columns: {
    name: 'Имя',
    email: 'Почта',
    role: 'Роль',
    addedBy: 'Добавлен',
    roleChangedBy: 'Роль изменена',
  },
  /** The dash of a role that never changed is for the eye; this is for the ear (design § 3.2). */
  roleNeverChanged: 'Роль не менялась',
  /** The row menu's items. */
  changeRole: 'Изменить роль',
  removeFromOrganization: 'Удалить из организации',
} as const;

/** The columns of the table in order, as the loading skeleton shows them too. */
export const MEMBERS_COLUMNS = [
  membersPage.columns.name,
  membersPage.columns.email,
  membersPage.columns.role,
  membersPage.columns.addedBy,
  membersPage.columns.roleChangedBy,
] as const;

/** «Добавление участника» (design § 3.5). */
export const addMemberDialog = {
  title: 'Добавление участника',
  submit: 'Добавить',
  userLabel: 'Пользователь',
  roleLabel: 'Роль',
} as const;

/** «Изменение роли» (design § 3.6). */
export const changeRoleDialog = {
  title: 'Изменение роли',
  submit: 'Сохранить',
  roleLabel: 'Роль',
  user: (name: string) => `Пользователь: ${name}`,
  selfDemotion: 'Вы потеряете права администратора. Вернуть их сможет только другой администратор.',
} as const;

/** The two confirmations of a removal (design § 3.7). */
export const removeMemberDialog = {
  submit: 'Удалить',
  otherTitle: 'Удалить участника?',
  selfTitle: 'Удалить себя из организации?',
  /** `name` is the plain name; `null`: an unknown user, who reads without «Пользователь». */
  otherText: (name: string | null, organizationName: string) =>
    `${name === null ? 'Неизвестный пользователь' : `Пользователь ${name}`} потеряет доступ к организации «${organizationName}» и будет исключён из всех её команд. Назначенные ему рабочие элементы сохранят исполнителя.`,
  selfText: (organizationName: string) =>
    `Вы потеряете доступ к организации «${organizationName}».`,
} as const;

/** The candidate list's hint lines, one at a time (design § 3.5). */
export const userSearchTexts = {
  tooShort: 'Введите не менее 2 символов имени или почты',
  searching: 'Поиск…',
  noMatch: 'Пользователи не найдены',
  moreMatched: 'Показаны первые 20. Уточните запрос.',
  failed: 'Не удалось выполнить поиск. Попробуйте ещё раз.',
} as const;

/** The notices of design § 3.8 and § 4.4; `name` is the plain name (no mark). */
export const memberNotices = {
  added: (name: string) => `Пользователь «${name}» добавлен в организацию.`,
  roleChanged: (name: string) => `Роль пользователя «${name}» изменена.`,
  removed: (name: string) => `Пользователь «${name}» удалён из организации.`,
  removedSelf: (organizationName: string) =>
    `Вы больше не состоите в организации «${organizationName}».`,
  addedToTeam: (name: string, teamName: string) =>
    `Пользователь «${name}» добавлен в команду «${teamName}».`,
  removedFromTeam: (name: string, teamName: string) =>
    `Пользователь «${name}» исключён из команды «${teamName}».`,
} as const;

/** Team members (design § 4). */
export const teamMembersSection = {
  title: 'Участники команды',
  add: 'Добавить в команду',
  tableLabel: 'Участники команды',
  columns: { name: 'Имя', email: 'Почта' },
  removeFromTeam: 'Исключить из команды',
  empty: {
    heading: 'В команде пока нет участников',
    adminHint: 'Добавьте участников организации кнопкой «Добавить в команду».',
    otherHint: 'Состав команды задаёт администратор организации.',
  },
} as const;

/** «Добавление в команду» (design § 4.4). */
export const addTeamMemberDialog = {
  title: 'Добавление в команду',
  submit: 'Добавить',
  userLabel: 'Участник организации',
  helper: 'Поиск среди активных участников организации',
} as const;
