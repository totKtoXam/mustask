/**
 * The context an address carries under mod-015 D-17's prefixes:
 * `/organizations/:organizationId/…` and
 * `/organizations/:organizationId/projects/:projectId/…`. An id is carried
 * only when a segment follows it — `/organizations/deactivated` and
 * `/organizations/:o` alone are not pages (design § 1) and carry nothing. A
 * team's page carries the organization and the project only: its team is the
 * page's entity, not a context level (design § 3.4). The team-level prefix
 * `…/teams/:t/…` carries the team too; no page of mod-002 lives there.
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
    if (segments[4] === 'teams' && segments[5] !== undefined && segments.length > 6) {
      context.teamId = decoded(segments[5]);
    }
  }
  return context;
}
