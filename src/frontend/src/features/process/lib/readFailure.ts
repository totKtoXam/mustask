import { organizationErrorCodes } from '../../../shared/api/errorCodes';
import { ApiError } from '../../../shared/api/httpClient';

/**
 * The read's one branch (api-contract § 1.3, design § States): «Организация
 * не найдена» only for an `ApiError` carrying the not-found code. Never the
 * status alone and never the text — a `404` with another code or no body, a
 * `5xx`, a `403`, a network error or an unreadable answer is a failed read.
 */
export function isOrganizationNotFound(error: unknown): boolean {
  return error instanceof ApiError && error.errorCode === organizationErrorCodes.notFound;
}
