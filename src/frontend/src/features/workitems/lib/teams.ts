// The owning team's words and options (mod-017 design §§ 3-5): the picker of the card and of
// the creation dialog, the filter of the list and of the project's views.
import type { ProjectTeam } from '../../organizations';
import { NO_TEAM_FILTER_VALUE } from '../api/workItemsApi';

/** The texts of the team controls, final in mod-017 design §§ 3-5 and nowhere else. */
export const teamTexts = {
  label: 'Команда',
  /** «Без команды» is a value, not a grey placeholder (design § 3). */
  noTeam: 'Без команды',
  /** The filter's text while nothing is chosen. */
  allTeams: 'Все команды',
  loading: 'Загрузка…',
  noMatch: 'Ничего не найдено',
  failed: 'Не удалось загрузить команды. Попробуйте ещё раз.',
} as const;

/** One choice of the picker: a team, or none (`id: null`). */
export interface TeamOption {
  id: string | null;
  name: string;
}

/** «Без команды», the picker's first choice and the value of an item that has no team. */
export const NO_TEAM_OPTION: TeamOption = { id: null, name: teamTexts.noTeam };

/**
 * The picker's options: «Без команды», then the project's teams as the read gives them — by
 * name, ignoring case (api-contract § 1.3), which the panel renders as received.
 */
export function teamOptions(teams: readonly Pick<ProjectTeam, 'id' | 'name'>[]): TeamOption[] {
  return [NO_TEAM_OPTION, ...teams.map(({ id, name }) => ({ id, name }))];
}

/**
 * The project's default team (BR-OT-4): the one the settings name, else the only team — the
 * list marks exactly that one — else none. A dialog opened away from a team's surface starts
 * with it.
 */
export function defaultTeamId(
  teams: readonly Pick<ProjectTeam, 'id' | 'defaultMark'>[],
): string | null {
  return teams.find((team) => team.defaultMark !== null)?.id ?? null;
}

/**
 * The filter's options (design § 5.2): the project's teams by name, then «Без команды» — whose
 * value is the contract's `none`. No counts.
 */
export function filterOptions(
  teams: readonly Pick<ProjectTeam, 'id' | 'name'>[],
): { value: string; label: string }[] {
  return [
    ...teams.map((team) => ({ value: team.id, label: team.name })),
    { value: NO_TEAM_FILTER_VALUE, label: teamTexts.noTeam },
  ];
}

/** The line under the creation dialog's field (design § 4); none for a type off the backlogs. */
export function backlogCaption(teamName: string | null): string {
  return teamName === null
    ? 'Попадёт только в бэклог проекта.'
    : `Встанет в конец бэклога команды «${teamName}».`;
}

/**
 * The line under «Команда» in a team view's creation dialog before a project is chosen (feat-040
 * design § 6.2, D-4): the field holds the view's team and is read-only, and the line says why.
 */
export function holdingCaption(teamName: string): string {
  return `Встанет в конец бэклога команды «${teamName}». Другую команду можно выбрать после выбора проекта.`;
}

/**
 * The line that takes the caption's place after a choice of project dropped the team shown (feat-040
 * design § 6.2, D-5): the team shown is not linked to the chosen project, and the view's team stands
 * again. It is also announced, politely, and stays until the next pick.
 */
export function teamDroppedLine(droppedTeam: string, project: string, keptTeam: string): string {
  return `Команда «${droppedTeam}» не участвует в проекте «${project}». Выбрана «${keptTeam}».`;
}

/** The area line of a team view's creation dialog before a project is chosen (feat-040 design § 6.2). */
export function noProjectAreaLine(stateName: string): string {
  return `Рабочий элемент будет создан в статусе «${stateName}». Остальные поля заполняются в его форме.`;
}

/** The reasons a created item is not in the rows of the view it was created on (feat-040 D-6). */
export const NOT_THIS_TEAM_REASON = 'Он не принадлежит этой команде.';
export const NOT_IN_FILTER_REASON = 'Он не подходит под текущий фильтр.';

/**
 * The notice after a creation on a team's view (feat-040 design § 6.4, D-6): today's, or the same
 * with the reason the item is not in the rows — it does not belong to this team (another team, or
 * none), else the project filter hides its project. The ids of the address are compared as the
 * server reads them: either letter case.
 */
export function createdNoticeOnTeamView(
  identifier: string,
  team: { id: string } | null,
  project: { id: string },
  view: { teamId: string; projectIds: readonly string[] },
): string {
  const created = `Рабочий элемент ${identifier} создан.`;
  if (team === null || team.id !== view.teamId) {
    return `${created} ${NOT_THIS_TEAM_REASON}`;
  }
  const hidden =
    view.projectIds.length > 0 &&
    !view.projectIds.some((candidate) => candidate.toLowerCase() === project.id.toLowerCase());
  return hidden ? `${created} ${NOT_IN_FILTER_REASON}` : created;
}

/** Whether an item of this owning team is in the filter (BR-OT-8): any chosen value matches. */
export function inFilter(team: { id: string } | null, teamIds: readonly string[]): boolean {
  return team === null ? teamIds.includes(NO_TEAM_FILTER_VALUE) : teamIds.includes(team.id);
}

/**
 * The notice after a creation (design § 4, § 6.5): today's, or — when a filter is set and the new
 * item's team is not in it — the same with the reason the item is not in the rows.
 */
export function createdNotice(
  identifier: string,
  team: { id: string } | null,
  teamIds: readonly string[],
): string {
  const created = `Рабочий элемент ${identifier} создан.`;
  return teamIds.length > 0 && !inFilter(team, teamIds)
    ? `${created} ${NOT_IN_FILTER_REASON}`
    : created;
}
