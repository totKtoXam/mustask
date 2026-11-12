import { organizationErrorCodes } from '../../../shared/api/errorCodes';
import { ApiError } from '../../../shared/api/httpClient';
import { ORGANIZATIONS_API } from '../api/organizationsApi';

/** An organization-scoped call that the server refused with 403 or 404. */
export interface ScopedFailure {
  organizationId: string;
  status: 403 | 404;
  /**
   * 404 `organizations.organization.not_found`: the organization is unknown,
   * deactivated, or the caller is not in it — one answer for all (api-contract
   * § 0.1 row 2). Another 404 (a project, a team) is not this.
   */
  organizationNotFound: boolean;
  /**
   * mod-019 (F-5): the object the call was about, when the path names one — a project, a team or a
   * department (`…/projects/<id>/…`). Its served codes may have changed; `own` marks the object's own
   * read or write (no segment after the id), whose failure must not re-read itself.
   */
  object?: { kind: ScopedObjectKind; id: string; own: boolean };
}

export type ScopedObjectKind = 'project' | 'team' | 'area';

const OBJECT_SEGMENTS: Readonly<Record<string, ScopedObjectKind>> = {
  projects: 'project',
  teams: 'team',
  areas: 'area',
};

/** A segment after the list that is a route of its own, not an object's id. */
const NOT_AN_OBJECT = new Set(['writable-ids', 'deactivated']);

const PREFIX = `${ORGANIZATIONS_API}/`;

/** The segment that is not an organization id but a list: `/api/organizations/deactivated`. */
const DEACTIVATED_LIST = 'deactivated';

/** The segment that is the access read itself: its 404 is an answer, not a failure (P-2). */
const ACCESS_READ = 'access';

/**
 * Whether `error` is the 403 or 404 of a call under
 * `/api/organizations/<id>/…` (plan-frontend P-4) — the failure whose
 * answer says the caller's access to that organization may have changed
 * (BR-M1-31) — and for which organization.
 *
 * `null` for everything else: an error that is not an `ApiError`, one with no
 * `path` (built by hand, or a lost session), any other status, the lists
 * `/api/organizations` and `/api/organizations/deactivated`, every other API
 * prefix, and the access read itself, whose 404 is its answer and any other
 * failure of which changes nothing.
 */
export function scopedFailureOf(error: unknown): ScopedFailure | null {
  if (!(error instanceof ApiError) || (error.status !== 403 && error.status !== 404)) {
    return null;
  }
  const { path } = error;
  if (path === undefined) {
    return null;
  }
  const withoutQuery = path.split('?', 1)[0] ?? '';
  if (!withoutQuery.startsWith(PREFIX)) {
    return null;
  }
  const [encodedId, ...rest] = withoutQuery.slice(PREFIX.length).split('/');
  if (!encodedId || encodedId === DEACTIVATED_LIST) {
    return null;
  }
  if (rest.length === 1 && rest[0] === ACCESS_READ) {
    return null;
  }
  let organizationId: string;
  try {
    organizationId = decodeURIComponent(encodedId);
  } catch {
    return null;
  }
  const failure: ScopedFailure = {
    organizationId,
    status: error.status,
    organizationNotFound:
      error.status === 404 && error.errorCode === organizationErrorCodes.notFound,
  };
  const kind = OBJECT_SEGMENTS[rest[0] ?? ''];
  const objectId = rest[1];
  if (kind !== undefined && objectId && !NOT_AN_OBJECT.has(objectId)) {
    try {
      failure.object = { kind, id: decodeURIComponent(objectId), own: rest.length === 2 };
    } catch {
      // An id that does not decode names no object.
    }
  }
  return failure;
}
