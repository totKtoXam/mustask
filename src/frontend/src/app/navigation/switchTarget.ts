import { HOME_PATH } from '../../shared/config/paths';
import type { AccessCheck } from '../../shared/lib/access';
import type { WorkContextSelection } from '../../shared/lib/workContext';
import type { NavigationEntry, NavigationModel } from './types';
import { entryMarker, isEntryVisible } from './visibility';

/** What a choice in the switcher asks (mod-002 D-o, `SwitchTargetResolver`). */
export interface SwitchRequest {
  pathname: string;
  carried: WorkContextSelection;
  next: WorkContextSelection;
}

function everyEntry(model: NavigationModel): NavigationEntry[] {
  return [...model.top, ...model.work, ...model.settings.flatMap((section) => section.entries)];
}

/**
 * Where a choice in the switcher moves the page (mod-002 design § 2.2, D-o;
 * mod-015 BR-18):
 *
 * 1. The entry the address is at or beneath, its landing built from the
 *    address's **own** ids — matched by the landing's pattern rather than by
 *    the switcher's context, so a not-found frame switches as the page it
 *    answers would, and a deactivated tab or a team's page find their entry.
 * 2. That entry's landing in the new context when it is visible there (a tab
 *    is a view, not an entry: the landing is the active tab); otherwise, or
 *    when no entry matches, «Главная».
 */
export function switchTarget(
  model: NavigationModel,
  access: AccessCheck,
  { pathname, carried, next }: SwitchRequest,
): string {
  const entry = everyEntry(model).find(
    (candidate) =>
      candidate.scope !== 'instance' && entryMarker(pathname, candidate, carried) !== null,
  );
  if (!entry || !isEntryVisible(entry, access, { isReal: true, selection: next })) {
    return HOME_PATH;
  }
  return entry.landing(next) ?? HOME_PATH;
}
