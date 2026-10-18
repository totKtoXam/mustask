// The membership side of the in-memory API (mod-016 plan-frontend § 2.8): the
// role-aware access read of api-contract § 1.1 and the scoped check of § 0.1
// in front of `serveOrganizations()`'s routes, so a test of a page by role
// meets the same answers in the same order the server gives. The routes of
// § 1.2–§ 2.4 (members, candidates, team members) are `testMembers.ts`'.
//
//   const api = serveOrganizations(data);
//   serveMemberships(data);   // after: MSW takes the newest handler first
import { HttpResponse, http, type HttpHandler } from 'msw';
import { server } from '../../test/mocks/server';
import { codesOf, servedFor } from '../../test/organizationRoles';
import { memberRoutes, type MembersApi, type MembersOptions } from './testMembers';
import {
  ORGANIZATIONS_URL,
  PROBLEM_HEADERS,
  problem,
  type Dataset,
  type MembershipRecord,
} from './testFixtures';

/** One row of api-contract § 4: the route and the code the scoped check holds the role to. */
interface RouteRule {
  method: string;
  /** The path after `/api/organizations/{organizationId}`, `''` for the organization itself. */
  pattern: RegExp;
  code: string;
  /** Row 7: the restore, the one route that accepts a deactivated organization. */
  acceptsDeactivated?: boolean;
}

const ID = '[^/]+';

function rule(
  method: string,
  path: string,
  code: string,
  options: { acceptsDeactivated?: boolean } = {},
): RouteRule {
  return { method, pattern: new RegExp(`^${path}$`), code, ...options };
}

/** api-contract § 4.1 and § 4.2 and the members of § 1 and § 2, route by route. */
const ROUTE_RULES: readonly RouteRule[] = [
  rule('GET', '', 'organizations.organization.read'),
  rule('GET', '/access', 'organizations.organization.read'),
  rule('PUT', '/name', 'organizations.organization.write'),
  rule('POST', '/deactivate', 'organizations.organization.deactivate'),
  rule('POST', '/restore', 'organizations.organization.deactivate', { acceptsDeactivated: true }),
  rule('GET', '/projects', 'organizations.project.read'),
  rule('GET', '/projects/deactivated', 'organizations.project.deactivate'),
  rule('POST', '/projects', 'organizations.project.write'),
  rule('GET', `/projects/${ID}`, 'organizations.project.read'),
  rule('PUT', `/projects/${ID}/name`, 'organizations.project.write'),
  rule('POST', `/projects/${ID}/deactivate`, 'organizations.project.deactivate'),
  rule('POST', `/projects/${ID}/restore`, 'organizations.project.deactivate'),
  rule('GET', `/projects/${ID}/teams`, 'organizations.team.read'),
  rule('POST', `/projects/${ID}/teams`, 'organizations.team.write'),
  rule('GET', `/projects/${ID}/teams/${ID}`, 'organizations.team.read'),
  rule('PUT', `/projects/${ID}/teams/${ID}/name`, 'organizations.team.write'),
  rule('GET', `/projects/${ID}/teams/${ID}/owned-items/count`, 'organizations.team.write'),
  rule('PUT', `/projects/${ID}/default-team`, 'organizations.team.write'),
  rule('DELETE', `/projects/${ID}/teams/${ID}`, 'organizations.team.write'),
  rule('GET', `/projects/${ID}/teams/${ID}/members`, 'organizations.team.read'),
  rule('GET', `/projects/${ID}/teams/${ID}/member-candidates`, 'organizations.team.write'),
  rule('POST', `/projects/${ID}/teams/${ID}/members`, 'organizations.team.write'),
  rule('DELETE', `/projects/${ID}/teams/${ID}/members/${ID}`, 'organizations.team.write'),
  rule('GET', `/projects/${ID}/areas`, 'organizations.area.read'),
  rule('POST', `/projects/${ID}/areas`, 'organizations.area.write'),
  rule('PUT', `/projects/${ID}/areas/${ID}/name`, 'organizations.area.write'),
  rule('PUT', `/projects/${ID}/areas/${ID}/parent`, 'organizations.area.write'),
  rule('DELETE', `/projects/${ID}/areas/${ID}`, 'organizations.area.write'),
  rule('GET', '/members', 'organizations.membership.read'),
  rule('GET', '/member-candidates', 'organizations.membership.write'),
  rule('POST', '/members', 'organizations.membership.write'),
  rule('PUT', `/members/${ID}/role`, 'organizations.membership.write'),
  rule('DELETE', `/members/${ID}`, 'organizations.membership.write'),
  rule('GET', '/process', 'process.definition.read'),
  rule('GET', `/projects/${ID}/workitems`, 'workitems.workitem.read'),
  rule('POST', `/projects/${ID}/workitems`, 'workitems.workitem.write'),
  rule('GET', `/projects/${ID}/workitems/${ID}`, 'workitems.workitem.read'),
  rule('PATCH', `/projects/${ID}/workitems/${ID}`, 'workitems.workitem.write'),
  // mod-005 api-contract § 5: Delete and Restore hold one code, Purge the administrator's.
  rule('POST', `/projects/${ID}/workitems/${ID}/delete`, 'workitems.workitem.delete'),
  rule('POST', `/projects/${ID}/workitems/${ID}/restore`, 'workitems.workitem.delete'),
  rule('POST', `/projects/${ID}/workitems/${ID}/purge`, 'workitems.workitem.purge'),
  rule('PUT', `/projects/${ID}/workitems/${ID}/parent`, 'workitems.link.write'),
  rule('GET', `/projects/${ID}/workitems/${ID}/parent-candidates`, 'workitems.workitem.read'),
  rule('GET', '/workitems/assignee-candidates', 'workitems.workitem.write'),
  rule('GET', `/projects/${ID}/teams/${ID}/backlogs/${ID}`, 'workitems.workitem.read'),
  rule('POST', `/projects/${ID}/teams/${ID}/backlogs/${ID}/reorder`, 'boards.backlog.reorder'),
  rule('GET', `/projects/${ID}/teams/${ID}/boards/${ID}`, 'workitems.workitem.read'),
];

/** The rule of a request, or `undefined` for a route the table does not know (the guard falls through). */
export function routeRuleOf(method: string, relativePath: string): RouteRule | undefined {
  const upper = method.toUpperCase();
  return ROUTE_RULES.find((entry) => entry.method === upper && entry.pattern.test(relativePath));
}

/** The code the scoped check holds `method` `relativePath` to, if the route is in the table. */
export function codeOfRoute(method: string, relativePath: string): string | undefined {
  return routeRuleOf(method, relativePath)?.code;
}

function refusal(code: string, status: number) {
  return HttpResponse.json(problem(code), { status, headers: PROBLEM_HEADERS });
}

/**
 * The acting user's membership in an organization of the dataset: absent
 * `memberships` makes the user the administrator of every organization, so a
 * dataset that never mentions memberships behaves as it did before mod-016.
 */
export function membershipOf(
  data: Dataset,
  organizationId: string | undefined,
): Pick<MembershipRecord, 'role'> | undefined {
  const id = organizationId?.toLowerCase();
  if (data.memberships === undefined) {
    return { role: 'admin' };
  }
  return data.memberships.find(
    (membership) =>
      membership.organizationId === id && membership.userId === (data.actingUserId ?? ''),
  );
}

/** Whether the dataset holds `organizationId` (and, unless `acceptsDeactivated`, as an active one). */
function knowsOrganization(
  data: Dataset,
  organizationId: string | undefined,
  acceptsDeactivated: boolean,
): boolean {
  const found = data.organizations.find(
    (organization) => organization.id === organizationId?.toLowerCase(),
  );
  return found !== undefined && (found.active || acceptsDeactivated);
}

/**
 * `GET …/access` (api-contract § 1.1): the acting user's role and its bundle,
 * or the 404 every non-member gets.
 */
export function accessRead(data: Dataset): HttpHandler {
  return http.get(`${ORGANIZATIONS_URL}/:organizationId/access`, ({ params }) => {
    const organizationId = String(params.organizationId);
    const membership = membershipOf(data, organizationId);
    if (!knowsOrganization(data, organizationId, false) || membership === undefined) {
      return refusal('organizations.organization.not_found', 404);
    }
    return HttpResponse.json(servedFor(membership.role));
  });
}

/**
 * The scoped check in front of every organization route (api-contract § 0.1):
 * 404 for an organization that is unknown, not active (the restore aside) or
 * not the caller's — the same answer for every cause — then 403 for a member
 * whose role lacks the route's code, before any nested id is judged. Anything
 * it lets through falls to the next handler, `serveOrganizations()`'s.
 */
export function scopedGuard(data: Dataset): HttpHandler {
  return http.all(`${ORGANIZATIONS_URL}/*`, ({ request }) => {
    const path = new URL(request.url).pathname.replace(/^\/api\/organizations\/?/, '');
    const [organizationId = '', ...rest] = path.split('/');
    if (organizationId === '' || organizationId === 'deactivated') {
      return undefined;
    }
    const matched = routeRuleOf(request.method, rest.length === 0 ? '' : `/${rest.join('/')}`);
    if (matched === undefined) {
      return undefined;
    }
    const membership = membershipOf(data, organizationId);
    if (
      !knowsOrganization(data, organizationId, matched.acceptsDeactivated === true) ||
      membership === undefined
    ) {
      return refusal('organizations.organization.not_found', 404);
    }
    if (!codesOf(membership.role).includes(matched.code)) {
      return refusal('organizations.access.forbidden', 403);
    }
    return undefined;
  });
}

/**
 * Installs the role-aware access read, the scoped guard and the member and
 * team-member routes for `data`'s memberships. Call it after
 * `serveOrganizations()`: MSW takes the newest handler first, and the guard
 * must see a request before the routes do. The handlers read `data` live, so
 * a test changes `data.memberships` (a role changed, a member removed) behind
 * an open page. Returns what the member routes received.
 */
export function serveMemberships(data: Dataset, options: MembersOptions = {}): MembersApi {
  const { handlers, api } = memberRoutes(data, options);
  server.use(accessRead(data), scopedGuard(data), ...handlers);
  return api;
}
