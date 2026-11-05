import AccountTreeOutlinedIcon from '@mui/icons-material/AccountTreeOutlined';
import DashboardOutlinedIcon from '@mui/icons-material/DashboardOutlined';
import FolderOutlinedIcon from '@mui/icons-material/FolderOutlined';
import FormatListNumberedIcon from '@mui/icons-material/FormatListNumbered';
import GroupsOutlinedIcon from '@mui/icons-material/GroupsOutlined';
import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import ListAltOutlinedIcon from '@mui/icons-material/ListAltOutlined';
import PeopleOutlineIcon from '@mui/icons-material/PeopleOutlined';
import ViewWeekOutlinedIcon from '@mui/icons-material/ViewWeekOutlined';
import type { SvgIconComponent } from '@mui/icons-material';
import { dictionariesPermissions, LANGUAGES_PATH } from '../../features/dictionaries';
import {
  ADMIN_ORGANIZATIONS_PATH,
  RESCUE_SECTION_LABEL,
  organizationGeneralPath,
  organizationsPermissions,
} from '../../features/organizations';
import { processDefinitionPath, processPermissions } from '../../features/process';
import { UI_KIT_PATH, uiKitPermissions } from '../../features/uikit';
import { HOME_PATH } from '../../shared/config/paths';
import { objectsOf, type WorkContextSelection } from '../../shared/lib/workContext';
import {
  KIND_ROUTE_PREFIX,
  OBJECT_SETTINGS_SEGMENT,
  SELECTOR_QUERY,
  menuFor,
  rowRoute,
  type MenuKind,
  type MenuRow,
} from './addressModel';
import { fullSelectionQuery, menuHref, menuKindOf } from './selection';
import type { ObjectSelection } from './selectorFit';
import type {
  MainNavigationEntry,
  NavigationEntry,
  NavigationModel,
  SettingsSection,
} from './types';

/** The label of the derived «Настройки» entry and of the settings navigation's title (BR-19). */
export const SETTINGS_LABEL = 'Настройки';

const ROW_ICONS: Record<string, SvgIconComponent> = {
  overview: DashboardOutlinedIcon,
  areas: AccountTreeOutlinedIcon,
  structure: AccountTreeOutlinedIcon,
  projects: FolderOutlinedIcon,
  teams: GroupsOutlinedIcon,
  members: PeopleOutlineIcon,
  general: InfoOutlinedIcon,
  workitems: ListAltOutlinedIcon,
  boards: ViewWeekOutlinedIcon,
  backlogs: FormatListNumberedIcon,
};

type ObjectKind = 'department' | 'team' | 'project';

const KIND_LABELS: Record<ObjectKind, string> = {
  department: 'Департамент',
  team: 'Команда',
  project: 'Проект',
};
const KIND_WRITE_CODES: Record<ObjectKind, string> = {
  department: organizationsPermissions.areaWrite,
  team: organizationsPermissions.teamWrite,
  project: organizationsPermissions.projectWrite,
};

function menuEntry(kind: MenuKind, row: MenuRow): MainNavigationEntry {
  const route = rowRoute(kind, row);
  return {
    id: `${kind}-${row.id}`,
    label: row.label,
    icon: ROW_ICONS[row.id] ?? DashboardOutlinedIcon,
    scope: 'organization',
    permission: row.code,
    landing: (selection) => {
      if (!selection.organizationId) return null;
      // «Структура» is the project's node, on the area prefix (ADR-0031 rule 5); a chosen team
      // is not dropped, it rides in the query (BR-NV-19).
      if (row.id === 'structure') {
        if (!selection.structureId) return null;
        const node = menuHref(
          selection.organizationId,
          { department: selection.structureId, team: null, project: null },
          { segment: 'overview' },
        );
        return selection.teamId
          ? `${node}?${SELECTOR_QUERY.team}=${encodeURIComponent(selection.teamId)}`
          : node;
      }
      return menuHref(selection.organizationId, objectsOf(selection), row);
    },
    pages: [route],
    beneath: [`${route}/*`],
  };
}

/**
 * The rows the shell builds the left menu from. «Структура» needs the project's node, which
 * the offers do not carry: the shell reads it from the project's subtree (an existing read) and
 * the entry is absent until it is known.
 */
function visibleRows(kind: MenuKind, selection: WorkContextSelection): readonly MenuRow[] {
  return menuFor(kind).filter((row) => row.id !== 'structure' || selection.structureId);
}

/** The selection up to a kind: itself and the less specific ones (ADR-0032 § 5, D-3). */
function selectionUpTo(objects: ObjectSelection, kind: ObjectKind): ObjectSelection {
  return {
    department: objects.department,
    team: kind === 'department' ? null : objects.team,
    project: kind === 'project' ? objects.project : null,
  };
}

/**
 * The query an organization's settings page carries: the whole selection, so the address names
 * the objects and the settings navigation shows their groups (QA F-3; ADR-0032 § 5).
 */
function settingsQuery(selection: WorkContextSelection): string {
  return fullSelectionQuery(objectsOf(selection));
}

function objectSettingsEntry(kind: ObjectKind, nodeOpen: boolean): NavigationEntry {
  return {
    id: `${kind}-settings`,
    label: 'Параметры',
    scope: 'organization',
    permission: KIND_WRITE_CODES[kind],
    landing: (selection) => {
      const chosen = selectionUpTo(objectsOf(selection), kind);
      return selection.organizationId && chosen[kind]
        ? menuHref(selection.organizationId, chosen, { segment: OBJECT_SETTINGS_SEGMENT })
        : null;
    },
    // On a node's own page `areas/:id/settings` is «Область», not the department.
    pages:
      kind === 'department' && nodeOpen
        ? []
        : [`${KIND_ROUTE_PREFIX[kind]}/${OBJECT_SETTINGS_SEGMENT}`],
  };
}

const HOME: MainNavigationEntry = {
  id: 'home',
  label: 'Главная',
  icon: HomeOutlinedIcon,
  scope: 'instance',
  landing: () => HOME_PATH,
  pages: [HOME_PATH],
};

/**
 * The navigation for a selection (feat-043 ADR-0032 § 2): «Главная», the menu of the most
 * specific chosen object, built from the address model's table, and the settings navigation —
 * «Организация», one group per chosen kind with «Параметры», then the instance sections.
 * «Настройки» is not an entry: it is shown when a settings entry is visible and opens the first
 * one in this order (BR-19).
 */
export function buildNavigationModel(selection: WorkContextSelection): NavigationModel {
  const objects = objectsOf(selection);
  const kind = menuKindOf(objects);
  const kindSections: SettingsSection[] = (['department', 'team', 'project'] as const)
    .filter((candidate) => objects[candidate] !== null)
    .map((candidate) => ({
      id: `settings-${candidate}`,
      label: KIND_LABELS[candidate],
      entries: [objectSettingsEntry(candidate, selection.nodeId !== undefined)],
    }));
  return {
    top: [HOME],
    work: visibleRows(kind, selection).map((row) => menuEntry(kind, row)),
    settings: [
      {
        id: 'organization',
        label: 'Организация',
        entries: [
          {
            id: 'organization-general',
            label: 'Общие',
            scope: 'organization',
            permission: organizationsPermissions.organizationRead,
            landing: (current) =>
              current.organizationId
                ? `${organizationGeneralPath(current.organizationId)}${settingsQuery(current)}`
                : null,
            pages: ['/organizations/:organizationId/settings/general'],
          },
          {
            id: 'process',
            label: 'Процесс',
            scope: 'organization',
            permission: processPermissions.definitionRead,
            landing: (current) =>
              current.organizationId
                ? `${processDefinitionPath(current.organizationId)}${settingsQuery(current)}`
                : null,
            pages: ['/organizations/:organizationId/settings/process'],
          },
        ],
      },
      ...kindSections,
      // While a project's node or a sub-area is open its own group (D-6), beside «Проект».
      ...(selection.nodeId
        ? [
            {
              id: 'settings-area',
              label: 'Область',
              entries: [
                {
                  id: 'area-settings',
                  label: 'Параметры',
                  scope: 'organization' as const,
                  permission: organizationsPermissions.areaWrite,
                  landing: (current: WorkContextSelection) =>
                    current.organizationId && current.nodeId
                      ? `/organizations/${encodeURIComponent(current.organizationId)}/areas/${encodeURIComponent(current.nodeId)}/${OBJECT_SETTINGS_SEGMENT}`
                      : null,
                  pages: [`${KIND_ROUTE_PREFIX.department}/${OBJECT_SETTINGS_SEGMENT}`],
                },
              ],
            },
          ]
        : []),
      {
        id: 'dictionaries',
        label: 'Справочники',
        entries: [
          {
            id: 'languages',
            label: 'Языки',
            scope: 'instance',
            permission: dictionariesPermissions.languagesRead,
            landing: () => LANGUAGES_PATH,
            pages: [LANGUAGES_PATH],
            beneath: [`${LANGUAGES_PATH}/*`],
          },
        ],
      },
      // The instance administrator's rescue list (feat-033 DC-2): a section of its own between the
      // dictionaries and the showcase, so «Настройки» still lands on the first visible entry as before.
      {
        id: 'administration',
        label: RESCUE_SECTION_LABEL,
        entries: [
          {
            id: 'all-organizations',
            label: 'Все организации',
            scope: 'instance',
            permission: organizationsPermissions.organizationRescue,
            landing: () => ADMIN_ORGANIZATIONS_PATH,
            pages: [ADMIN_ORGANIZATIONS_PATH],
          },
        ],
      },
      // The UI kit's showcase (tech-015 design § 10.1): last, so «Настройки» still lands on
      // the first visible entry as before. Shown only to the holders of the showcase's code.
      {
        id: 'interface',
        label: 'Интерфейс',
        entries: [
          {
            id: 'ui-kit',
            label: 'Витрина компонентов',
            scope: 'instance',
            permission: uiKitPermissions.showcaseRead,
            landing: () => UI_KIT_PATH,
            pages: [UI_KIT_PATH],
          },
        ],
      },
    ],
  };
}
