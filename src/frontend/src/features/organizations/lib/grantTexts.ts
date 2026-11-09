// The words of mod-019 (design.md §§ 2-7), exactly as the design quotes them. Roles by decision OQ-10:
// «Читатель», «Участник», «Менеджер» in every scope. A string no earlier design quotes is marked ◆ there.
import type { GrantRole, GrantScope } from '../api/grantsApi';

export const GRANT_ROLES: readonly GrantRole[] = ['reader', 'member', 'manager'];

/** The lowest role: the dialog opens on it on every scope, the team's included (D-7). */
export const LOWEST_ROLE: GrantRole = 'reader';

export const GRANT_ROLE_LABELS: Readonly<Record<GrantRole, string>> = {
  reader: 'Читатель',
  member: 'Участник',
  manager: 'Менеджер',
};

/** The helper under «Роль» by scope and chosen role (design § 3). */
export const GRANT_ROLE_HELPERS: Readonly<Record<GrantScope, Readonly<Record<GrantRole, string>>>> =
  {
    department: {
      reader: 'Просматривает проекты департамента и их рабочие элементы.',
      member:
        'Всё, что у «Читателя», а также создание и изменение рабочих элементов проектов департамента.',
      manager:
        'Всё, что у «Участника», а также управление департаментом, его проектами и областями: названия, доступ, связи, создание проектов.',
    },
    project: {
      reader: 'Просматривает проект.',
      member: 'Всё, что у «Читателя», а также рабочие элементы проекта и порядок его бэклога.',
      manager:
        'Всё, что у «Участника», а также настройки проекта, доступ, связи и области проекта.',
    },
    team: {
      reader:
        'Только числится в списке «Участники и доступ» команды; бэклог и спринты команды видят все, порядок и спринты не меняет.',
      member: 'Упорядочивает бэклог команды и ведёт её спринты.',
      manager: 'Всё, что у «Участника», а также участники, название, связи команды и доступ к ней.',
    },
  };

export const grantsSection = {
  titleDepartment: 'Участники',
  titleObject: 'Участники и доступ',
  teamSectionMembers: 'Участники команды',
  teamSectionAccess: 'Доступ к команде',
  give: 'Выдать доступ',
  filter: 'Найти участника',
  tableLabel: 'Доступ',
  columns: { user: 'Участник', role: 'Роль', origin: 'Откуда', givenBy: 'Выдан' },
  rowMenu: (name: string) => `Ещё действия: ${name}`,
  changeRole: 'Изменить роль',
  revoke: 'Отозвать доступ',
  you: 'Вы',
  held: 'В проекте',
  fromDepartment: (name: string) => `От департамента «${name}»`,
  noticeOnlyGranted:
    'Здесь показан только выданный доступ. Роль в организации действует везде и задаётся на странице «Участники» организации.',
  noticeSwitchedOff: (department: string) =>
    `Доступ департамента «${department}» в этом проекте отключён.`,
  loadFailed: 'Не удалось загрузить доступ.',
  retry: 'Повторить',
  empty: {
    department: 'В департаменте пока никому не выдан доступ',
    project: 'В проекте пока никому не выдан доступ',
    team: 'К команде пока никому не выдан доступ',
    managerLine: 'Выдайте роль, чтобы человек работал с объектом.',
  },
  nothingFound: 'Ничего не найдено',
  resetFilter: 'Сбросить фильтр',
  teamRoleColumn: 'Роль в команде',
  noTeamRole: '—',
  noTeamRoleForEar: 'Роли в команде нет',
} as const;

export const giveDialog = {
  title: 'Выдача доступа',
  submit: 'Выдать',
  userLabel: 'Участник организации',
  roleLabel: 'Роль',
  nobody:
    'Пользователи не найдены. Себе доступ выдать нельзя; у кого он уже есть, тому роль меняется в списке.',
  selfGrant: 'Нельзя выдать доступ самому себе.',
  wrongScope: 'Эту роль нельзя выдать на этом объекте.',
  memberNotFound: 'Участник не найден.',
  forbidden: 'Недостаточно прав',
  close: 'Закрыть',
  given: (name: string) => `Пользователю «${name}» выдан доступ.`,
} as const;

export const changeGrantDialog = {
  title: 'Изменение роли',
  submit: 'Сохранить',
  user: 'Пользователь: ',
  changed: (name: string) => `Роль пользователя «${name}» изменена.`,
  gone: 'Доступ уже изменён или отозван. Список обновлён.',
} as const;

export const revokeDialog = {
  confirm: 'Отозвать',
  titleOther: 'Отозвать доступ?',
  titleSelf: 'Отозвать свой доступ?',
  other: (name: string, role: string, object: string) =>
    `Пользователь ${name} потеряет роль «${role}» на объекте «${object}». Доступ, который он получает по роли в организации или от департамента, сохранится.`,
  self: (role: string, object: string) =>
    `Вы потеряете роль «${role}» на объекте «${object}». Вернуть её сможет другой менеджер или администратор организации.`,
  doneOther: (name: string) => `Доступ пользователя «${name}» отозван.`,
  doneSelf: 'Ваш доступ отозван.',
} as const;

/** «Наследовать доступ» (design § 4). */
export const inheritAccess = {
  label: 'Наследовать доступ',
  section: 'Доступ',
  on: 'Включено',
  off: 'Выключено',
  turnedOn: 'Наследование доступа включено.',
  turnedOff: 'Наследование доступа отключено.',
  forbidden: 'Недостаточно прав',
  stateText: {
    nodeOn: (department: string, project: string) =>
      `Доступ департамента «${department}» действует в проекте «${project}», его рабочих элементах и областях.`,
    nodeOff: (department: string, project: string) =>
      `Доступ департамента «${department}» в проекте «${project}» не действует. Действует только доступ, выданный в самом проекте.`,
    topLevel:
      'Над проектом нет департамента, поэтому выбор пока ни на что не влияет. Он начнёт действовать, когда проект переместят в департамент.',
    subAreaOn: (parent: string) =>
      `Права на управление областями, действующие в «${parent}», действуют и здесь. На рабочие элементы это не влияет.`,
    subAreaOff: (parent: string) =>
      `Права на управление областями из «${parent}» здесь не действуют. На рабочие элементы это не влияет: их определяет проект.`,
  },
} as const;

/** The count's plural forms: 1 пользователь, 2-4 пользователя, 0 and 5+ пользователей (design § 5). */
export function usersCount(count: number): string {
  const lastTwo = count % 100;
  const last = count % 10;
  const word =
    lastTwo >= 11 && lastTwo <= 14
      ? 'пользователей'
      : last === 1
        ? 'пользователь'
        : last >= 2 && last <= 4
          ? 'пользователя'
          : 'пользователей';
  return `${count} ${word}`;
}

/** The switch-off confirmation (design § 5, OQ-6, BR-SA-7, BR-SA-19, R-27). */
export const switchOffDialog = {
  title: 'Отключить наследование доступа?',
  confirm: 'Отключить',
  leadNode: (department: string, project: string) =>
    `Доступ департамента «${department}» перестанет действовать в проекте «${project}», его рабочих элементах и областях. Доступ, выданный в самом проекте, сохранится.`,
  leadSubArea: (parent: string, area: string) =>
    `Права на управление областями из «${parent}» перестанут действовать в области «${area}» и её вложенных областях. На рабочие элементы это не влияет.`,
  countNode: (count: number) => `Потеряют доступ через департамент: ${usersCount(count)}.`,
  countSubArea: (count: number) => `Потеряют право управлять областью: ${usersCount(count)}.`,
  countNote: 'Не учитываются администраторы организации и те, у кого есть доступ в самом проекте.',
  nobody: 'Это не отнимет доступ ни у кого.',
  selfNode:
    'Вы входите в их число. После отключения вы потеряете управление этим узлом и не сможете включить наследование обратно: это сможет сделать только администратор организации.',
  selfSubArea:
    'Вы входите в их число. После отключения вы потеряете управление этим узлом и не сможете включить наследование обратно: это сможет сделать менеджер проекта или администратор организации.',
  givenHeading: (count: number) => `Доступ в проекте, выданный этими пользователями (${count})`,
  givenTable: 'Выданный доступ',
  givenColumns: { user: 'Кому', role: 'Роль', givenBy: 'Кто выдал' },
  givenNote:
    'Он не прекратится при отключении. Отозвать его можно вручную в «Участники и доступ» проекта.',
  loadFailed: 'Не удалось узнать, кого это затронет. Попробуйте ещё раз.',
  retry: 'Повторить',
  forbidden: 'Недостаточно прав',
} as const;

/** One notice per object when a re-read shows other codes (design § 7); no role is named. */
export const codesChangedNotice = {
  project: (name: string) => `Ваш доступ в проекте «${name}» изменился.`,
  department: (name: string) => `Ваш доступ в департаменте «${name}» изменился.`,
  team: (name: string) => `Ваш доступ в команде «${name}» изменился.`,
} as const;
