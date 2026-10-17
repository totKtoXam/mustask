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
 *
 * The check is asked about `next`'s organization, and when that
 * organization's codes are not read yet (`isCodesRead`) it is not asked at
 * all: the switch does not wait for them (mod-016 design § 2.5, DC-8), every
 * entry's code being a read code all three roles hold, so a switch from
 * «Участники» of A opens «Участники» of B whatever B's codes. The page then
 * runs the gate's state order.
 *
 * One branch before them (feat-039 BR-PB-9): a project's own backlog or board
 * (an entry's `projectViews`) is opened again in the new project at the same
 * level, not moved to the entry's landing, so a switch of project or
 * organization does not leave the whole-project view. It needs the new
 * context's project and nothing below it — a project page names no team, and
 * the switcher's team is not asked — and «Главная» is the answer when there is
 * none. A switch of the team never reaches here: the address carries no team,
 * so the page stays (mod-015 BR-18).
 */
export function switchTarget(
  model: NavigationModel,
  access: AccessCheck,
  { pathname, carried, next }: SwitchRequest,
  isCodesRead: (organizationId: string | undefined) => boolean = () => true,
): string {
  const projectEntry = everyEntry(model).find((candidate) =>
    candidate.projectViews?.matches(pathname),
  );
  if (projectEntry?.projectViews) {
    const allowed =
      projectEntry.permission === undefined ||
      !isCodesRead(next.organizationId) ||
      access(projectEntry.permission, projectEntry.scope, next.organizationId);
    return (allowed && projectEntry.projectViews.retarget(pathname, next)) || HOME_PATH;
  }
  const entry = everyEntry(model).find(
    (candidate) =>
      candidate.scope !== 'instance' && entryMarker(pathname, candidate, carried) !== null,
  );
  if (
    !entry ||
    !isEntryVisible(entry, access, {
      isReal: true,
      selection: next,
      skipServedCodes: !isCodesRead(next.organizationId),
    })
  ) {
    return HOME_PATH;
  }
  return entry.landing(next) ?? HOME_PATH;
}
