import type { UserRef } from '../api/membershipApi';

/** A user the projection does not know yet (mod-014 BR-UP-17; design § 3.3). */
export const UNKNOWN_USER = 'Неизвестный пользователь';

/** The mark of a user whose Keycloak account is disabled or deleted (design § 3.3). */
export const INACTIVE_MARK = 'неактивен';

/**
 * A user named in plain text — a notice, a confirmation's sentence, a row
 * button's accessible name — by the display name alone, without the mark; an
 * unknown user as «Неизвестный пользователь» (design § 3.3, § 3.8).
 */
export function plainName(user: Pick<UserRef, 'displayName' | 'state'>): string {
  return user.state === 'unknown' || !user.displayName ? UNKNOWN_USER : user.displayName;
}
