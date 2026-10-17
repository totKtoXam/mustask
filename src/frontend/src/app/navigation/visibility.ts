import type { AccessCheck } from '../../shared/lib/access';
import type { WorkContextSelection } from '../../shared/lib/workContext';
import type { TrailSegment } from '../../shared/ui/pageChrome';
import { SETTINGS_LABEL } from './model';
import type {
  MainNavigationEntry,
  NavigationEntry,
  NavigationModel,
  NavigationScope,
  SettingsSection,
} from './types';

/** What visibility needs of the context source (BR-8, D-18). */
export interface NavigationContext {
  isReal: boolean;
  selection: WorkContextSelection;
  /**
   * The switch resolver's question about an organization whose codes are not
   * read yet (mod-016 design § 2.5, DC-8): the code of a non-instance entry is
   * not asked — every entry's code is a read code all three roles hold — and
   * the entry's page existing and the context being real still decide. The
   * shell's own navigation never sets it: unread codes hide the entries
   * (design § 6.1).
   */
  skipServedCodes?: boolean;
}

export interface VisibleSettingsSection {
  section: SettingsSection;
  entries: readonly NavigationEntry[];
}

/** The model filtered for this user and this context; empty groups dropped (BR-7). */
export interface VisibleNavigation {
  top: readonly MainNavigationEntry[];
  work: readonly MainNavigationEntry[];
  settings: readonly VisibleSettingsSection[];
  /** «Настройки» opens this: the first visible settings entry; `null` hides it (BR-19). */
  settingsLanding: string | null;
}

function scopeSelected(scope: NavigationScope, selection: WorkContextSelection): boolean {
  switch (scope) {
    case 'instance':
      return true;
    case 'organization':
      return Boolean(selection.organizationId);
    case 'project':
      return Boolean(selection.organizationId && selection.projectId);
    case 'team':
      return Boolean(selection.organizationId && selection.projectId && selection.teamId);
  }
}

/**
 * An entry is shown only when its page is registered, the shared check
 * passes for its code, and — for an organization-, project- or team-scoped
 * entry — the context it needs is real (BR-7, BR-8, D-18). The placeholder
 * source is never real, so a scoped entry stays hidden in a build without a
 * real context.
 *
 * The check is asked about the selection's organization (mod-016 design
 * § 2.6): the codes of an organization are served per organization, and an
 * organization whose codes are unread, failed or answered 404 answers "no".
 */
export function isEntryVisible(
  entry: NavigationEntry,
  access: AccessCheck,
  context: NavigationContext,
): boolean {
  const skipCode = context.skipServedCodes === true && entry.scope !== 'instance';
  if (
    entry.permission !== undefined &&
    !skipCode &&
    !access(entry.permission, entry.scope, context.selection.organizationId)
  ) {
    return false;
  }
  if (entry.scope === 'instance') {
    return true;
  }
  return (
    context.isReal &&
    scopeSelected(entry.scope, context.selection) &&
    entry.landing(context.selection) !== null
  );
}

export function visibleModel(
  model: NavigationModel,
  access: AccessCheck,
  context: NavigationContext,
): VisibleNavigation {
  const visible = (entry: NavigationEntry) => isEntryVisible(entry, access, context);
  const settings = model.settings
    .map((section) => ({ section, entries: section.entries.filter(visible) }))
    .filter((section) => section.entries.length > 0);
  const firstSettingsEntry = settings[0]?.entries[0];
  return {
    top: model.top.filter(visible),
    work: model.work.filter(visible),
    settings,
    settingsLanding: firstSettingsEntry ? firstSettingsEntry.landing(context.selection) : null,
  };
}

/**
 * `aria-current` for an entry (US-3): `page` on its landing page, `true` on
 * any page beneath it, none elsewhere.
 */
export type EntryMarker = 'page' | 'true' | null;

/**
 * The address as the router serves it: trailing slashes name the same page
 * (`/home/` is `/home`), not a page beneath it.
 */
function pagePath(pathname: string): string {
  return pathname.replace(/\/+$/, '') || '/';
}

/**
 * `currentPage` is the address of the page on screen, or `null` while a
 * not-found frame answers the address: no page is current then, so no entry
 * is marked (BR-13). An entry's sibling views (mod-006's level tabs) are its
 * page as the landing is.
 */
export function entryMarker(
  currentPage: string | null,
  entry: NavigationEntry,
  selection: WorkContextSelection,
): EntryMarker {
  const landing = entry.landing(selection);
  if (!landing || currentPage === null) {
    return null;
  }
  const path = pagePath(currentPage);
  // feat-039 BR-PB-9: a project's own page of the entry is the entry's page as its landing
  // is — and only the selection's project's: a page of another is none of this selection's.
  if (entry.projectViews?.matches(path) && entry.projectViews.retarget(path, selection) === path) {
    return 'page';
  }
  const pages = [landing, ...(entry.views?.(selection) ?? [])];
  if (pages.includes(path)) {
    return 'page';
  }
  return pages.some((page) => path.startsWith(`${page}/`)) ? 'true' : null;
}

/**
 * Whether the page on screen is a settings page: the landing of an entry the
 * user sees, or a page beneath it (BR-19; mod-016 design § 6.1). An address no
 * page serves (`null`, see `entryMarker`) is not one, whatever it starts with,
 * and neither is a page that no *visible* entry matches — an organization's
 * settings page while its codes are unread: it shows no settings navigation,
 * no marker and no trail until the read answers. The settings navigation is
 * shown on it only while at least one settings entry is visible.
 */
export function isSettingsPage(
  currentPage: string | null,
  visible: VisibleNavigation,
  selection: WorkContextSelection,
): boolean {
  return visible.settings.some(({ entries }) =>
    entries.some((entry) => entryMarker(currentPage, entry, selection) !== null),
  );
}

/**
 * The page trail the shell derives from the model for the current address
 * (BR-6, plan § 4.5, D-7): on a settings page «Настройки» and the section,
 * both plain text, then the entry as a link when the page is beneath its
 * landing; beneath a main entry's landing, that entry as a link. A page's own
 * deeper ancestors follow (`PageHeader`'s `ancestors`).
 */
export function deriveTrail(
  pathname: string,
  visible: VisibleNavigation,
  selection: WorkContextSelection,
): TrailSegment[] {
  for (const { section, entries } of visible.settings) {
    for (const entry of entries) {
      const marker = entryMarker(pathname, entry, selection);
      if (marker !== null) {
        const trail: TrailSegment[] = [{ label: SETTINGS_LABEL }, { label: section.label }];
        if (marker === 'true') {
          trail.push({ label: entry.label, to: entry.landing(selection) ?? undefined });
        }
        return trail;
      }
    }
  }
  for (const entry of [...visible.top, ...visible.work]) {
    if (entryMarker(pathname, entry, selection) === 'true') {
      return [{ label: entry.label, to: entry.landing(selection) ?? undefined }];
    }
  }
  return [];
}
