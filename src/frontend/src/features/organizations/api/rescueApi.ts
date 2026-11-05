// The rescue routes over HTTP, strictly per docs/features/feat-033-orphaned-organization-rescue/
// api-contract.md § 1 and § 2.1: one function per route, camelCase as all of Mustask.Api. The list
// is paged by the server at 25 (no `pageSize`), the record is a plain array newest first.
import { apiFetch } from '../../../shared/api/httpClient';
import type { UserCandidates, UserRef } from './membershipApi';
import { ORGANIZATIONS_API, organizationApiPath } from './organizationsApi';

/** § 1.1: the server's fixed page size (BR-RS-2). */
export const RESCUE_PAGE_SIZE = 25;

export type RescueStatus = 'active' | 'deactivated';

/** § 1.1: one row of «Все организации». Nothing about administrators (BR-RS-2). */
export interface RescueOrganization {
  id: string;
  name: string;
  status: RescueStatus;
  /** `null`: no appointment yet. */
  lastAppointedAt: string | null;
}

export interface RescuePage {
  count: number;
  data: RescueOrganization[];
}

/** § 1.4 and § 2.1: one record of «История назначений». */
export interface Appointment {
  id: string;
  appointedAt: string;
  appointedBy: UserRef;
  appointee: UserRef;
  reason: string;
}

export interface AppointBody {
  userId: string;
  /** Trimmed by the dialog; the server trims it as well. */
  reason: string;
}

const RESCUE_API = `${ORGANIZATIONS_API}/rescue`;

function rescueOrganizationPath(organizationId: string): string {
  return `${RESCUE_API}/${encodeURIComponent(organizationId)}`;
}

/** § 1.1: `page` is 1-based; `name` is sent trimmed and left out when empty. */
export function getRescueOrganizations(page: number, name: string): Promise<RescuePage> {
  const query = new URLSearchParams({ page: String(page) });
  const trimmed = name.trim();
  if (trimmed !== '') {
    query.set('name', trimmed);
  }
  return apiFetch(`${RESCUE_API}?${query}`);
}

/** § 1.2: under 2 characters the server answers nobody; the panel does not ask. */
export function searchAppointeeCandidates(text: string): Promise<UserCandidates> {
  const query = new URLSearchParams({ text });
  return apiFetch(`${RESCUE_API}/appointee-candidates?${query}`);
}

/** § 1.3: resolves with the record just written (201). */
export function appointAdministrator(
  organizationId: string,
  body: AppointBody,
): Promise<Appointment> {
  return apiFetch(`${rescueOrganizationPath(organizationId)}/appointments`, {
    method: 'POST',
    body,
  });
}

/** § 1.4: the record read by the holder of the instance code. */
export function getRescueAppointments(organizationId: string): Promise<Appointment[]> {
  return apiFetch(`${rescueOrganizationPath(organizationId)}/appointments`);
}

/** § 2.1: the record read by the organization's own administrator. */
export function getOrganizationAppointments(organizationId: string): Promise<Appointment[]> {
  return apiFetch(`${organizationApiPath(organizationId)}/appointments`);
}
