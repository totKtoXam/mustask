import { createContext, useContext } from 'react';

/** A notice of the slice (design § 4.5): plain text, or an error with the server's text. */
export interface OrganizationsNotice {
  text: string;
  severity: 'success' | 'error';
}

/** Shows the slice's one notice; a newer one replaces it. */
export type Notify = (text: string, severity?: OrganizationsNotice['severity']) => void;

const ignore: Notify = () => {};

/**
 * The slice's notice host, `OrganizationsSection`, provides it around the
 * slice's routes, so a notice outlives the move to the next page (create →
 * «Проекты», deactivate → the deactivated tab, delete from a team's page →
 * «Команды»). Outside it a notice goes nowhere.
 */
export const NotifyContext = createContext<Notify>(ignore);

export function useNotify(): Notify {
  return useContext(NotifyContext);
}
