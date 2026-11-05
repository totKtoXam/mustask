// An in-memory copy of the rescue routes (feat-033 api-contract § 1 and § 2.1) for the tests of
// «Все организации», the appointment dialog and «История назначений»: 120 organizations (the
// stories' dataset, design § 11), the appointee search over a few users, the answer order of
// § 1.3 and the record, newest first. A test reads `requests` or puts a one-off answer in front.
import { HttpResponse, http, type HttpHandler } from 'msw';
import { server } from '../../test/mocks/server';
import type { UserRef } from './api/membershipApi';
import type { Appointment, RescueOrganization } from './api/rescueApi';
import { ORGANIZATIONS_URL, PROBLEM_HEADERS, problem, validationProblem } from './testFixtures';

export const ACTING = 'user-acting';

export const VERA: UserRef = {
  id: 'user-vera',
  displayName: 'Вера Иванова',
  email: 'vera@example.org',
  state: 'active',
};
export const BORIS: UserRef = {
  id: 'user-boris',
  displayName: 'Борис Петров',
  email: 'boris@example.org',
  state: 'active',
};
export const ROOT_ADMIN: UserRef = {
  id: ACTING,
  displayName: 'Системный Администратор',
  email: 'admin@example.org',
  state: 'active',
};

export const rescueIds = {
  deactivated: '00000000-0000-4000-8000-000000000002',
  appointed: '00000000-0000-4000-8000-000000000003',
  plain: '00000000-0000-4000-8000-000000000001',
} as const;

const LAST_APPOINTED = '2026-11-03T11:05:00+00:00';

function idOf(position: number): string {
  return `00000000-0000-4000-8000-${String(position).padStart(12, '0')}`;
}

/** 117 numbered organizations and three «Гамма»: 120, so three pages of 25 and a last of 20 — five. */
export function rescueOrganizations(): RescueOrganization[] {
  const numbered = Array.from({ length: 117 }, (_unused, index): RescueOrganization => {
    const position = index + 1;
    return {
      id: idOf(position),
      name: `Организация ${String(position).padStart(3, '0')}`,
      status: position === 2 ? 'deactivated' : 'active',
      lastAppointedAt: position === 3 ? LAST_APPOINTED : null,
    };
  });
  const gammas = ['Гамма Север', 'Гамма Юг', 'Ёлка Гамма'].map(
    (name, index): RescueOrganization => ({
      id: idOf(200 + index),
      name,
      status: 'active',
      lastAppointedAt: null,
    }),
  );
  return [...gammas, ...numbered].sort((first, second) =>
    first.name.localeCompare(second.name, 'ru'),
  );
}

export interface RecordedRescueRequest {
  method: string;
  path: string;
  search: string;
  body?: unknown;
}

export interface RescueOptions {
  organizations?: RescueOrganization[];
  /** The record per organization id, newest first. */
  records?: Record<string, Appointment[]>;
  /** The users the appointee search finds (active ones). */
  users?: UserRef[];
  /** Ids that are already administrators: the appointment answers `already_admin`. */
  admins?: string[];
  /** The caller's `sub`, for the self refusal. */
  subject?: string;
}

const REASON_MAX = 500;

function normalized(text: string): string {
  return text.trim().toLowerCase().replaceAll('ё', 'е');
}

function answer(code: string): Response {
  const body = problem(code);
  return HttpResponse.json(body, { status: body.status, headers: PROBLEM_HEADERS });
}

/** Serves the rescue routes; the newest handlers win, so call it after the other serve helpers. */
export function serveRescue(options: RescueOptions = {}) {
  const organizations = options.organizations ?? rescueOrganizations();
  const records = options.records ?? {};
  const users = options.users ?? [VERA, BORIS];
  const admins = new Set(options.admins ?? []);
  const subject = options.subject ?? ACTING;
  const requests: RecordedRescueRequest[] = [];

  async function record(request: Request): Promise<void> {
    const url = new URL(request.url);
    const text = request.method === 'GET' ? '' : await request.clone().text();
    requests.push({
      method: request.method,
      path: url.pathname,
      search: url.search,
      body: text === '' ? undefined : (JSON.parse(text) as unknown),
    });
  }

  const handlers: HttpHandler[] = [
    http.get(`${ORGANIZATIONS_URL}/rescue`, async ({ request }) => {
      await record(request);
      const query = new URL(request.url).searchParams;
      const page = Number(query.get('page') ?? '1');
      const needle = normalized(query.get('name') ?? '');
      const matched = organizations.filter(
        (organization) => needle === '' || normalized(organization.name).includes(needle),
      );
      return HttpResponse.json({
        count: matched.length,
        data: matched.slice((page - 1) * 25, page * 25),
      });
    }),
    http.get(`${ORGANIZATIONS_URL}/rescue/appointee-candidates`, async ({ request }) => {
      await record(request);
      const needle = normalized(new URL(request.url).searchParams.get('text') ?? '');
      const found =
        needle.length < 2
          ? []
          : users.filter(
              (user) =>
                user.state === 'active' &&
                (normalized(user.displayName ?? '').includes(needle) ||
                  normalized(user.email ?? '').includes(needle)),
            );
      return HttpResponse.json({
        users: found.map((user) => ({
          id: user.id,
          displayName: user.displayName ?? '',
          email: user.email ?? '',
        })),
        moreMatched: false,
      });
    }),
    http.post(
      `${ORGANIZATIONS_URL}/rescue/:organizationId/appointments`,
      async ({ request, params }) => {
        await record(request);
        const organizationId = String(params.organizationId);
        const organization = organizations.find((entry) => entry.id === organizationId);
        if (!organization) {
          return answer('organizations.organization.not_found');
        }
        const body = (await request.clone().json()) as { userId: string; reason: string };
        const appointee = users.find((user) => user.id === body.userId);
        if (!appointee) {
          return answer('organizations.rescue.user_unavailable');
        }
        if (body.userId === subject) {
          return answer('organizations.rescue.self_appointment');
        }
        if (admins.has(`${organizationId}:${body.userId}`)) {
          return answer('organizations.rescue.already_admin');
        }
        const reason = body.reason.trim();
        const length = Array.from(reason).length;
        if (length === 0 || length > REASON_MAX) {
          const code =
            length === 0
              ? 'organizations.rescue.reason_required'
              : 'organizations.rescue.reason_too_long';
          return HttpResponse.json(validationProblem({ reason: [code] }), {
            status: 400,
            headers: PROBLEM_HEADERS,
          });
        }
        const written: Appointment = {
          id: `record-${requests.length}`,
          appointedAt: new Date().toISOString(),
          appointedBy: ROOT_ADMIN,
          appointee,
          reason,
        };
        records[organizationId] = [written, ...(records[organizationId] ?? [])];
        organization.lastAppointedAt = written.appointedAt;
        admins.add(`${organizationId}:${body.userId}`);
        return HttpResponse.json(written, { status: 201 });
      },
    ),
    http.get(
      `${ORGANIZATIONS_URL}/rescue/:organizationId/appointments`,
      async ({ request, params }) => {
        await record(request);
        return HttpResponse.json(records[String(params.organizationId)] ?? []);
      },
    ),
    http.get(`${ORGANIZATIONS_URL}/:organizationId/appointments`, async ({ request, params }) => {
      await record(request);
      return HttpResponse.json(records[String(params.organizationId)] ?? []);
    }),
  ];
  server.use(...handlers);
  return { requests, organizations, records };
}

/** Puts `response` in front of `GET …/rescue` (a 403, a 500, an empty page…) for the next tests' calls. */
export function failRescueList(response: Response | (() => Response)): void {
  server.use(
    http.get(`${ORGANIZATIONS_URL}/rescue`, () =>
      typeof response === 'function' ? response() : response.clone(),
    ),
  );
}

/** A problem answer by catalog code, with the catalog's status and text. */
export function problemResponse(code: string, detail?: string): Response {
  const body = problem(code);
  return HttpResponse.json(detail === undefined ? body : { ...body, detail }, {
    status: body.status,
    headers: PROBLEM_HEADERS,
  });
}
