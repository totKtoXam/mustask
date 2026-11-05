import type { SvgIconComponent } from '@mui/icons-material';
import type { PermissionScope } from '../../shared/lib/access';
import type { WorkContextSelection } from '../../shared/lib/workContext';

/** The level an entry's page lives at (design § 4.1). */
export type NavigationScope = PermissionScope;

/**
 * One navigation entry (feat-043 ADR-0032 § 2). The model is built from the address model for
 * the current selection (`buildNavigationModel`), so being in it is what "the page exists" means:
 * an entry is absent, not disabled, until a line adds its row. A settings entry carries no icon.
 */
export interface NavigationEntry {
  id: string;
  label: string;
  scope: NavigationScope;
  /** The code the shared check must pass; none: any signed-in user. */
  permission?: string;
  /**
   * The entry's landing address in the current context; `null` when the context lacks the
   * level the scope needs.
   */
  landing(selection: WorkContextSelection): string | null;
  /**
   * The route patterns that are the entry's page (marked `page`), matched by the router's own
   * `matchPath` (bug-003: case and percent-encoding ignored as the router ignores them). A
   * board or backlog entry lists its `:level` pattern.
   */
  pages: readonly string[];
  /** The patterns of pages beneath the entry (marked `true`): a work item beneath its list. */
  beneath?: readonly string[];
}

/**
 * An entry of «Основное меню»: «Главная» and the menu of the most specific chosen object. The
 * icon is required, because the rail shows nothing else (design § 4.3); a main entry registered
 * without one does not compile.
 */
export interface MainNavigationEntry extends NavigationEntry {
  icon: SvgIconComponent;
}

/** A section of «Меню настроек»: «Организация», «Проект», «Справочники» (BR-19). */
export interface SettingsSection {
  id: string;
  label: string;
  entries: readonly NavigationEntry[];
}

/** The whole navigation, in model order (BR-19's "first settings page" follows it). */
export interface NavigationModel {
  /** «Главная». */
  top: readonly MainNavigationEntry[];
  /** The sections of the most specific chosen object (the left menu). */
  work: readonly MainNavigationEntry[];
  settings: readonly SettingsSection[];
}
