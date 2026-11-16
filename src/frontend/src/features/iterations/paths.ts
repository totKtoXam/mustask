// The slice's addresses (design § 1): below `/organizations/:o`, under the team's prefix. Ids are
// untyped route parameters: a page never validates them, the builders re-encode them as received.

export const TEAM_SPRINTS_ROUTE = '/organizations/:organizationId/teams/:teamId/sprints';
export const SPRINT_OUTCOME_ROUTE = `${TEAM_SPRINTS_ROUTE}/:sprintId`;

const segment = encodeURIComponent;

/** «Спринты» of a team: the navigation entry's landing. */
export function teamSprintsPath(organizationId: string, teamId: string): string {
  return `/organizations/${segment(organizationId)}/teams/${segment(teamId)}/sprints`;
}

/** A Closed sprint's outcome page. */
export function sprintOutcomePath(
  organizationId: string,
  teamId: string,
  sprintId: string,
  search = '',
): string {
  return `${teamSprintsPath(organizationId, teamId)}/${segment(sprintId)}${search}`;
}

/** A work item's own page, by its project (duplicated from the work items slice on purpose). */
export function outcomeItemPath(
  organizationId: string,
  projectId: string,
  identifier: string,
): string {
  return `/organizations/${segment(organizationId)}/projects/${segment(projectId)}/workitems/${segment(identifier)}`;
}
