// The owning team's words and options (mod-017 design §§ 3-5): the picker of the card and of
// the creation dialog, the filter of the list and of the project's views.
import type { Team } from '../../organizations';
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
export function teamOptions(teams: readonly Pick<Team, 'id' | 'name'>[]): TeamOption[] {
  return [NO_TEAM_OPTION, ...teams.map(({ id, name }) => ({ id, name }))];
}

/**
 * The project's default team (BR-OT-4): the one the settings name, else the only team — the
 * list marks exactly that one — else none. A dialog opened away from a team's surface starts
 * with it.
 */
export function defaultTeamId(teams: readonly Pick<Team, 'id' | 'defaultMark'>[]): string | null {
  return teams.find((team) => team.defaultMark !== null)?.id ?? null;
}

/**
 * The filter's options (design § 5.2): the project's teams by name, then «Без команды» — whose
 * value is the contract's `none`. No counts.
 */
export function filterOptions(
  teams: readonly Pick<Team, 'id' | 'name'>[],
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
    ? `${created} Он не подходит под текущий фильтр.`
    : created;
}
