import type { SvgIconComponent } from '@mui/icons-material';
import type { PermissionScope } from '../../shared/lib/access';
import type { WorkContextSelection } from '../../shared/lib/workContext';

/** The level an entry's page lives at (design § 4.1). */
export type NavigationScope = PermissionScope;

/**
 * One navigation entry (design § 4.1). Being in the model is what "its page
 * exists in the build" means (BR-7, plan § 9 D-19): a line adds its entry
 * when it builds the page. A settings entry is exactly this: it carries no
 * icon (design § 4.1).
 */
export interface NavigationEntry {
  id: string;
  label: string;
  scope: NavigationScope;
  /** The code the shared check must pass; none: any signed-in user. */
  permission?: string;
  /**
   * The entry's landing address in the current context; `null` when the
   * context lacks the level the scope needs (BR-18 prefixes).
   */
  landing(selection: WorkContextSelection): string | null;
  /**
   * The addresses of the entry's page's other views, when they are siblings
   * of the landing rather than beneath it — mod-006's level tabs
   * (`…/backlogs/epics` beside the landing `…/backlogs/stories`). Each is the
   * entry's page as the landing is: marked `page`, no trail, and the switcher
   * moves it to the landing in the new context. Absent: the landing alone.
   */
  views?(selection: WorkContextSelection): readonly string[];
}

/**
 * An entry of «Основное меню»: «Главная» and the work hubs. The icon is
 * required, because the rail shows nothing else (design § 4.3); a main entry
 * registered without one does not compile.
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
  /** The work hubs: «Рабочие элементы», «Доски», «Бэклоги»… — as their pages are built. */
  work: readonly MainNavigationEntry[];
  settings: readonly SettingsSection[];
}
