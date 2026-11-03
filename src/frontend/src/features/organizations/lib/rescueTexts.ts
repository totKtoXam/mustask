// Every string of «Все организации», «Назначение администратора» and «История назначений» that the
// spec and design.md § 10 (feat-033) put in the panel, in one place so no component types one
// (frontend.md "Forms"). The strings the spec quotes are verbatim; the ones marked (added) are the
// design's.
import { organizationErrorCodes as codes } from '../../../shared/api/errorCodes';

/** The section of the settings navigation and the page's trail (added). */
export const RESCUE_SECTION_LABEL = 'Администрирование';

/** The page «Все организации» (design § 3, § 10). */
export const rescuePage = {
  title: 'Все организации',
  filterLabel: 'Поиск по названию',
  columns: {
    organization: 'Организация',
    status: 'Статус',
    lastAppointed: 'Последнее назначение',
  },
  statusActive: 'Активна',
  statusDeactivated: 'Деактивирована',
  /** The dash of a never-appointed row is for the eye; this is for the ear. */
  neverAppointed: 'Назначений не было',
  empty: 'Организаций пока нет',
  noMatch: 'Организации не найдены',
  resetFilter: 'Сбросить фильтр',
  /** The live region after a filter settles (added). */
  found: (count: number) => `Найдено: ${count}`,
  appoint: 'Назначить администратора',
  history: 'История назначений',
} as const;

/** The columns in order, as the loading skeleton shows them too (the last is the actions). */
export const RESCUE_COLUMNS = [
  rescuePage.columns.organization,
  rescuePage.columns.status,
  rescuePage.columns.lastAppointed,
  '',
] as const;

/** «Назначение администратора» (design § 4). */
export const appointDialog = {
  title: 'Назначение администратора',
  submit: 'Назначить',
  /** `name` is the row's, without truncation. */
  lead: (name: string) =>
    `Выбранный пользователь станет администратором организации «${name}». Никто не будет удалён, ничья роль не будет понижена. Вы не получите доступа к содержимому организации.`,
  keycloakWarning:
    'MUSTASK не видит, отключена ли учётная запись в Keycloak: убедитесь, что пользователь может войти.',
  userLabel: 'Пользователь',
  reasonLabel: 'Причина',
  /** The helper while there is no error, and the over-length error. */
  reasonLimit: 'Не более 500 символов',
  reasonRequired: 'Укажите причину',
  organizationMissing: 'Организация не найдена',
  userUnavailable: 'Этого пользователя нельзя назначить администратором',
  selfAppointment: 'Нельзя назначить администратором себя',
  alreadyAdmin: 'Пользователь уже администратор этой организации',
  forbidden: 'Недостаточно прав',
  /** `name` is the appointee's display name. */
  appointed: (name: string) => `Администратор назначен: ${name}`,
} as const;

/** «История назначений» (design § 5). */
export const historyDialog = {
  /** `name` is the organization's. */
  title: (name: string) => `История назначений — «${name}»`,
  tableLabel: 'История назначений',
  columns: {
    when: 'Когда',
    by: 'Кто назначил',
    appointee: 'Кого назначили',
    reason: 'Причина',
  },
  empty: 'Назначений не было',
  close: 'Закрыть',
  forbidden: 'Недостаточно прав',
  notFound: 'Организация не найдена',
} as const;

/** The columns in order, as the loading skeleton shows them too. */
export const HISTORY_COLUMNS = [
  historyDialog.columns.when,
  historyDialog.columns.by,
  historyDialog.columns.appointee,
  historyDialog.columns.reason,
] as const;

/**
 * The Russian text of each refusal code (`contracts/errors/organizations.json`, `ru`), keyed by
 * `errorCode` — the panel branches on the code and never on the server's wording (frontend.md
 * "Named values"). A refusal the table does not hold falls back to the server's own text.
 */
export const RESCUE_REFUSAL_TEXTS: Readonly<Record<string, string>> = {
  [codes.rescueForbidden]: appointDialog.forbidden,
  [codes.rescueUserUnavailable]: appointDialog.userUnavailable,
  [codes.rescueSelfAppointment]: appointDialog.selfAppointment,
  [codes.rescueAlreadyAdmin]: appointDialog.alreadyAdmin,
  [codes.rescueReasonRequired]: appointDialog.reasonRequired,
  [codes.rescueReasonTooLong]: appointDialog.reasonLimit,
  [codes.rescueReasonInvalid]: 'Причина содержит недопустимые символы',
  [codes.rescueInvalidPage]: 'Некорректный номер страницы.',
  [codes.notFound]: appointDialog.organizationMissing,
};
