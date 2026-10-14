// The member and team-member routes of the in-memory API (mod-016 plan-frontend
// § 2.8, lane B1; api-contract § 1.2–§ 1.6 and § 2.1–§ 2.4): lists in the
// contract's order, the candidate searches cut at 20, the add, role change
// and removal with their 400 and 409 answers in the contract's order, and the
// last-administrator rule; and the organization lists narrowed to the
// caller's memberships (§ 4.1 rows 1–2). They sit behind the scoped guard of
// `testMembership.ts` (which has judged the caller by the time a request gets
// here) and read and write the dataset's `users`, `memberships` and
// `teamMembers`, so a flow test sees its own writes and a test changes the
// dataset behind an open page.
import { HttpResponse, http, type HttpHandler } from 'msw';
import type { Member, UserCandidates, UserRef } from './api/membershipApi';
import { ORGANIZATION_ROLES, type OrganizationRole } from './lib/roles';
import type { RecordedRequest } from './testApi';
import {
  ORGANIZATIONS_URL,
  PROBLEM_HEADERS,
  problem,
  validationProblem,
  type Dataset,
  type MembershipRecord,
  type TeamRecord,
} from './testFixtures';

/** When a membership with no stamp of its own was added. */
const SEEDED_AT = '2026-10-01T10:00:00+00:00';

/** What the member routes record, for "nothing was sent" and "this was sent" assertions. */
export interface MembersApi {
  requests: RecordedRequest[];
}

export interface MembersOptions {
  /** The time a new membership or role change is stamped with; the clock by default. */
  now?(): string;
}

const TEAM_URL = `${ORGANIZATIONS_URL}/:organizationId/projects/:projectId/teams/:teamId`;

function answer(code: string): Response {
  const body = problem(code);
  return HttpResponse.json(body, { status: body.status, headers: PROBLEM_HEADERS });
}

function invalid(reasons: Record<string, readonly string[]>): Response {
  const body = validationProblem(reasons);
  return HttpResponse.json(body, { status: body.status, headers: PROBLEM_HEADERS });
}

function codePointOrder(first: string, second: string): number {
  return first < second ? -1 : first > second ? 1 : 0;
}

/** mod-014 BR-UP-15's order, unknown users last by id (api-contract § 0.5). */
function directoryOrder(first: UserRef, second: UserRef): number {
  const unknownFirst = first.state === 'unknown';
  if (unknownFirst !== (second.state === 'unknown')) {
    return unknownFirst ? 1 : -1;
  }
  if (unknownFirst) {
    return codePointOrder(first.id, second.id);
  }
  return (
    (first.displayName ?? '').localeCompare(second.displayName ?? '', 'ru', {
      sensitivity: 'base',
    }) || codePointOrder(first.id, second.id)
  );
}

function userRef(data: Dataset, id: string): UserRef {
  const found = data.users?.find((candidate) => candidate.id === id);
  return found
    ? { id, displayName: found.displayName, email: found.email, state: found.state }
    : { id, displayName: null, email: null, state: 'unknown' };
}

function memberOf(data: Dataset, record: MembershipRecord): Member {
  return {
    user: userRef(data, record.userId),
    role: record.role,
    addedBy: userRef(data, record.addedBy ?? data.actingUserId ?? ''),
    addedAt: record.addedAt ?? SEEDED_AT,
    roleChangedBy: record.roleChangedBy ? userRef(data, record.roleChangedBy) : null,
    roleChangedAt: record.roleChangedAt ?? null,
  };
}

function isRole(value: unknown): value is OrganizationRole {
  return typeof value === 'string' && (ORGANIZATION_ROLES as readonly string[]).includes(value);
}

function isUserId(value: unknown): value is string {
  return typeof value === 'string' && value.trim() !== '' && value.length <= 255;
}

/** The text the way mod-014 normalizes it for a search: trimmed, case ignored. */
function candidatesFor(data: Dataset, text: string, allowed: (id: string) => boolean) {
  const needle = text.trim().toLowerCase();
  if (needle.length < 2) {
    return { users: [], moreMatched: false } satisfies UserCandidates;
  }
  const matched = (data.users ?? [])
    .filter(
      (user) =>
        user.state === 'active' &&
        allowed(user.id) &&
        ((user.displayName ?? '').toLowerCase().includes(needle) ||
          (user.email ?? '').toLowerCase().includes(needle)),
    )
    .map((user) => userRef(data, user.id))
    .sort(directoryOrder);
  return {
    users: matched.slice(0, 20).map((user) => ({
      id: user.id,
      displayName: user.displayName ?? '',
      email: user.email ?? '',
    })),
    moreMatched: matched.length > 20,
  } satisfies UserCandidates;
}

/** The known, active administrators of an organization, `except` aside (BR-M1-4, BR-M1-32). */
function activeAdminsLeft(data: Dataset, organizationId: string, except: string): number {
  return (data.memberships ?? []).filter(
    (membership) =>
      membership.organizationId === organizationId &&
      membership.role === 'admin' &&
      membership.userId !== except &&
      userRef(data, membership.userId).state === 'active',
  ).length;
}

/**
 * The routes of api-contract § 1.2–§ 1.6 and § 2.1–§ 2.4 over `data`, in
 * front of `serveOrganizations()`'s and behind the scoped guard.
 */
export function memberRoutes(
  data: Dataset,
  { now = () => new Date().toISOString() }: MembersOptions = {},
): { handlers: HttpHandler[]; api: MembersApi } {
  const requests: RecordedRequest[] = [];
  const record = async (request: Request) => {
    const url = new URL(request.url);
    let body: unknown;
    if (request.method !== 'GET' && request.method !== 'DELETE') {
      body = await request
        .clone()
        .json()
        .catch(() => undefined);
    }
    requests.push({ method: request.method, path: `${url.pathname}${url.search}`, body });
  };
  const members = () => (data.memberships ??= []);
  const teamMembers = () => (data.teamMembers ??= []);
  const inOrganization = (organizationId: string) =>
    members().filter((membership) => membership.organizationId === organizationId);
  const idOf = (value: unknown) => String(value).toLowerCase();

  /** The team of the address, or the not-found answer of its outermost missing level. */
  function teamOf(params: Record<string, unknown>): TeamRecord | Response {
    const project = data.projects.find(
      (candidate) =>
        candidate.id === idOf(params.projectId) &&
        candidate.organizationId === idOf(params.organizationId) &&
        candidate.active,
    );
    if (!project) {
      return answer('organizations.project.not_found');
    }
    return (
      data.teams.find(
        (candidate) =>
          candidate.id === idOf(params.teamId) &&
          candidate.projectId === project.id &&
          !candidate.deleted,
      ) ?? answer('organizations.team.not_found')
    );
  }

  /** The organizations of one status the caller is in, and — for the deactivated list — administers (§ 4.1 rows 1–2). */
  function organizationsOf(active: boolean) {
    return data.organizations
      .filter((organization) => organization.active === active)
      .filter((organization) => {
        const role = members().find(
          (membership) =>
            membership.organizationId === organization.id &&
            membership.userId === data.actingUserId,
        )?.role;
        return role !== undefined && (active || role === 'admin');
      })
      .sort(
        (first, second) =>
          first.name.localeCompare(second.name, 'ru', { sensitivity: 'base' }) ||
          codePointOrder(first.id, second.id),
      )
      .map(({ id, name }) => ({ id, name }));
  }

  const handlers: HttpHandler[] = [
    // § 4.1 rows 1–2: the lists hold the caller's organizations only.
    http.get(ORGANIZATIONS_URL, () => HttpResponse.json(organizationsOf(true))),
    http.get(`${ORGANIZATIONS_URL}/deactivated`, () => HttpResponse.json(organizationsOf(false))),
    // § 1.2
    http.get(`${ORGANIZATIONS_URL}/:organizationId/members`, async ({ request, params }) => {
      await record(request);
      const rows = inOrganization(idOf(params.organizationId)).map((membership) =>
        memberOf(data, membership),
      );
      return HttpResponse.json(rows.sort((a, b) => directoryOrder(a.user, b.user)));
    }),
    // § 1.3
    http.get(`${ORGANIZATIONS_URL}/:organizationId/member-candidates`, async ({ request }) => {
      await record(request);
      const text = new URL(request.url).searchParams.get('text') ?? '';
      return HttpResponse.json(candidatesFor(data, text, () => true));
    }),
    // § 1.4
    http.post(`${ORGANIZATIONS_URL}/:organizationId/members`, async ({ request, params }) => {
      await record(request);
      const body = (await request.clone().json()) as Record<string, unknown>;
      const organizationId = idOf(params.organizationId);
      const reasons: Record<string, string[]> = {};
      if (!isUserId(body.userId)) {
        reasons.userId = ['organizations.membership.user_invalid'];
      }
      if (!isRole(body.role)) {
        reasons.role = ['organizations.membership.role_invalid'];
      }
      if (Object.keys(reasons).length > 0) {
        return invalid(reasons);
      }
      const userId = body.userId as string;
      if (userRef(data, userId).state !== 'active') {
        return answer('organizations.membership.user_unavailable');
      }
      if (inOrganization(organizationId).some((membership) => membership.userId === userId)) {
        return answer('organizations.membership.already_member');
      }
      const created: MembershipRecord = {
        organizationId,
        userId,
        role: body.role as OrganizationRole,
        addedAt: now(),
        addedBy: data.actingUserId,
        roleChangedAt: null,
        roleChangedBy: null,
      };
      members().push(created);
      return HttpResponse.json(memberOf(data, created), {
        status: 201,
        headers: { Location: `${ORGANIZATIONS_URL}/${organizationId}/members` },
      });
    }),
    // § 1.5
    http.put(
      `${ORGANIZATIONS_URL}/:organizationId/members/:userId/role`,
      async ({ request, params }) => {
        await record(request);
        const body = (await request.clone().json()) as Record<string, unknown>;
        if (!isRole(body.role)) {
          return invalid({ role: ['organizations.membership.role_invalid'] });
        }
        const organizationId = idOf(params.organizationId);
        const userId = String(params.userId);
        const target = inOrganization(organizationId).find(
          (membership) => membership.userId === userId,
        );
        if (!target) {
          return answer('organizations.membership.not_found');
        }
        if (target.role === body.role) {
          return HttpResponse.json(memberOf(data, target));
        }
        if (target.role === 'admin' && activeAdminsLeft(data, organizationId, userId) === 0) {
          return answer('organizations.membership.last_admin');
        }
        target.role = body.role;
        target.roleChangedAt = now();
        target.roleChangedBy = data.actingUserId ?? null;
        return HttpResponse.json(memberOf(data, target));
      },
    ),
    // § 1.6
    http.delete(
      `${ORGANIZATIONS_URL}/:organizationId/members/:userId`,
      async ({ request, params }) => {
        await record(request);
        const organizationId = idOf(params.organizationId);
        const userId = String(params.userId);
        const target = inOrganization(organizationId).find(
          (membership) => membership.userId === userId,
        );
        if (!target) {
          return answer('organizations.membership.not_found');
        }
        if (target.role === 'admin' && activeAdminsLeft(data, organizationId, userId) === 0) {
          return answer('organizations.membership.last_admin');
        }
        data.memberships = members().filter((membership) => membership !== target);
        // BR-M1-36: the user leaves every team of the organization.
        const teamIds = new Set(
          data.teams
            .filter((team) =>
              data.projects.some(
                (project) =>
                  project.id === team.projectId && project.organizationId === organizationId,
              ),
            )
            .map((team) => team.id),
        );
        data.teamMembers = teamMembers().filter(
          (entry) => !(entry.userId === userId && teamIds.has(entry.teamId)),
        );
        return new HttpResponse(null, { status: 204 });
      },
    ),
    // § 2.1
    http.get(`${TEAM_URL}/members`, async ({ request, params }) => {
      await record(request);
      const team = teamOf(params);
      if (team instanceof Response) {
        return team;
      }
      const rows = teamMembers()
        .filter((entry) => entry.teamId === team.id)
        .map((entry) => userRef(data, entry.userId));
      return HttpResponse.json(rows.sort(directoryOrder));
    }),
    // § 2.2
    http.get(`${TEAM_URL}/member-candidates`, async ({ request, params }) => {
      await record(request);
      const team = teamOf(params);
      if (team instanceof Response) {
        return team;
      }
      const organizationId = idOf(params.organizationId);
      const text = new URL(request.url).searchParams.get('text') ?? '';
      return HttpResponse.json(
        candidatesFor(data, text, (id) =>
          inOrganization(organizationId).some((membership) => membership.userId === id),
        ),
      );
    }),
    // § 2.3
    http.post(`${TEAM_URL}/members`, async ({ request, params }) => {
      await record(request);
      const body = (await request.clone().json()) as Record<string, unknown>;
      if (!isUserId(body.userId)) {
        return invalid({ userId: ['organizations.membership.user_invalid'] });
      }
      const team = teamOf(params);
      if (team instanceof Response) {
        return team;
      }
      const organizationId = idOf(params.organizationId);
      const userId = body.userId;
      const isMember = inOrganization(organizationId).some(
        (membership) => membership.userId === userId,
      );
      if (!isMember || userRef(data, userId).state !== 'active') {
        return answer('organizations.team.member_not_in_organization');
      }
      if (teamMembers().some((entry) => entry.teamId === team.id && entry.userId === userId)) {
        return answer('organizations.team.member_exists');
      }
      teamMembers().push({ teamId: team.id, userId });
      return HttpResponse.json(userRef(data, userId), {
        status: 201,
        headers: { Location: `${new URL(request.url).pathname}` },
      });
    }),
    // § 2.4
    http.delete(`${TEAM_URL}/members/:userId`, async ({ request, params }) => {
      await record(request);
      const team = teamOf(params);
      if (team instanceof Response) {
        return team;
      }
      const userId = String(params.userId);
      const entry = teamMembers().find(
        (candidate) => candidate.teamId === team.id && candidate.userId === userId,
      );
      if (!entry) {
        return answer('organizations.team.member_not_found');
      }
      data.teamMembers = teamMembers().filter((candidate) => candidate !== entry);
      return new HttpResponse(null, { status: 204 });
    }),
  ];

  return { handlers, api: { requests } };
}
