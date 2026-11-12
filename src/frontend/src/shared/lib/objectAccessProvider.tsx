import type { ReactNode } from 'react';
import { AccessCheckContext } from './access';
import { useObjectAccess } from './objectAccess';

/**
 * Wraps the body of a page that shows one object: everything below — the page's own `useAccessCheck()`, its
 * sections and the dialogs' `RequiresOrganizationCode` locks — then asks the object's served codes for the
 * codes decided at that object (BR-SA-18), and the organization answer for the rest.
 */
export function ObjectAccess({
  codes,
  children,
}: {
  codes: readonly string[] | null | undefined;
  children: ReactNode;
}) {
  const check = useObjectAccess(codes);
  return <AccessCheckContext.Provider value={check}>{children}</AccessCheckContext.Provider>;
}
