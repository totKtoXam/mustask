import { dictionariesPermissions } from '../dictionaries';

/**
 * The showcase's gate (tech-015 design § 10.1, status.md D-5). The page is for
 * administrators and the panel decides by an instance permission code in the access
 * token (ADR-0018); the showcase reuses the one the dictionaries' administrators
 * already hold, so no backend, realm or stand changes. It is one constant: a code of
 * its own would change this line (and the realm, and every stand).
 */
export const uiKitPermissions = {
  showcaseRead: dictionariesPermissions.languagesWrite,
} as const;
