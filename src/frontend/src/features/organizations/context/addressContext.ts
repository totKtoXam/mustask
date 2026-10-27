/**
 * The context an address carries under mod-015 D-17's prefixes:
 * `/organizations/:organizationId/…`,
 * `/organizations/:organizationId/projects/:projectId/…` and, since feat-040 (BR-TS-1), a
 * team's views at `/organizations/:organizationId/teams/:teamId/…`. An id is carried
 * only when a segment follows it — `/organizations/deactivated` and
 * `/organizations/:o` alone are not pages (design § 1) and carry nothing. A team
 * address carries the organization and the team and no project: the project level shows its
 * current selection, and choosing a project there moves nothing (ADR-0024 § 2). The retired
 * project-nested `…/projects/:p/teams/:t/…` carries the project only: its page answers
 * «Страница не найдена» and names no team.
 *
 * Ids are returned decoded, as the lists hold them, and never validated.
 */
export interface AddressContext {
  organizationId?: string;
  projectId?: string;
  teamId?: string;
}

function decoded(segment: string): string {
  try {
    return decodeURIComponent(segment);
  } catch {
    return segment;
  }
}

export function addressContext(pathname: string): AddressContext {
  const segments = pathname.split('/').filter((part) => part !== '');
  if (segments[0] !== 'organizations' || segments.length < 3 || segments[1] === undefined) {
    return {};
  }
  const context: AddressContext = { organizationId: decoded(segments[1]) };
  if (segments[2] === 'projects' && segments[3] !== undefined && segments.length > 4) {
    context.projectId = decoded(segments[3]);
  } else if (segments[2] === 'teams' && segments[3] !== undefined && segments.length > 4) {
    context.teamId = decoded(segments[3]);
  }
  return context;
}
