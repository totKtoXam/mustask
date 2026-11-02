import { NO_TEAM_FILTER_VALUE } from '../../workitems';
import type { BoardScope } from './scope';

/**
 * A backlog narrowed by a selector keeps its rank order and offers no reorder (feat-043 US-4,
 * Q-9 A; ADR-0032 § 5; design § 7.1). In this line it is the project's backlog under a chosen
 * team; the texts are final in design §§ 4, 7.1.
 */
export const narrowedTexts = {
  hint: 'Пока выбор сужает список, порядок не меняется. Выберите «все», чтобы менять его.',
  withoutTeam: 'без команды',
} as const;

/** Whether the view shows a subset of one rank order, so its order cannot be changed. */
export function isNarrowed(scope: BoardScope, filterIds: readonly string[]): boolean {
  return scope.kind === 'project' && filterIds.length > 0;
}

/**
 * The tag in the title line: «сужено: команда Платформа». A team the shell does not know (its
 * list is not read, or the team is gone) is left out of the names; with none left the tag says
 * only «сужено».
 */
export function narrowedTag(
  filterIds: readonly string[],
  teams: readonly { id: string; name: string }[],
): string {
  const names = filterIds.flatMap((id) => {
    const known = teams.find((team) => team.id.toLowerCase() === id.toLowerCase());
    return known ? [known.name] : [];
  });
  // «без команды» is not a team: it stands without «команда», alone or after the names.
  const none = filterIds.includes(NO_TEAM_FILTER_VALUE);
  if (names.length === 0) {
    return none ? `сужено: ${narrowedTexts.withoutTeam}` : 'сужено';
  }
  const teamsText = `команда ${names.join(', ')}`;
  return `сужено: ${none ? `${teamsText}, ${narrowedTexts.withoutTeam}` : teamsText}`;
}
