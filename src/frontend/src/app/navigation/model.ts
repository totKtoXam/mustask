import FormatListNumberedIcon from '@mui/icons-material/FormatListNumbered';
import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined';
import ListAltOutlinedIcon from '@mui/icons-material/ListAltOutlined';
import ViewWeekOutlinedIcon from '@mui/icons-material/ViewWeekOutlined';
import {
  backlogLevelPaths,
  backlogsLandingPath,
  boardLevelPaths,
  boardsLandingPath,
  boardsPermissions,
  projectBacklogLevelOf,
  projectBacklogPath,
  projectBoardLevelOf,
  projectBoardPath,
} from '../../features/boards';
import { dictionariesPermissions, LANGUAGES_PATH } from '../../features/dictionaries';
import {
  areasPath,
  membersPath,
  organizationGeneralPath,
  organizationTeamsPath,
  organizationsPermissions,
  projectGeneralPath,
  projectsPath,
  teamsPath,
} from '../../features/organizations';
import { processDefinitionPath, processPermissions } from '../../features/process';
import { UI_KIT_PATH, uiKitPermissions } from '../../features/uikit';
import { workItemsPath, workItemsPermissions } from '../../features/workitems';
import { HOME_PATH } from '../../shared/config/paths';
import type { NavigationModel, ProjectViews } from './types';

/** The label of the derived «Настройки» entry and of the settings navigation's title (BR-19). */
export const SETTINGS_LABEL = 'Настройки';

/**
 * A project's page of an entry (feat-039 BR-PB-9): the address `levelOf` reads a level
 * from, and the same page at that level in the selection's project, which `pathOf` builds.
 */
function projectViews<Level extends string>(
  levelOf: (pathname: string) => Level | null,
  pathOf: (organizationId: string, projectId: string, level: Level) => string,
): ProjectViews {
  return {
    matches: (pathname) => levelOf(pathname) !== null,
    retarget: (pathname, selection) => {
      const level = levelOf(pathname);
      return level !== null && selection.organizationId && selection.projectId
        ? pathOf(selection.organizationId, selection.projectId, level)
        : null;
    },
  };
}

/**
 * The navigation registry (mod-015 design § 4.1, BR-7). Only pages that
 * exist in the build are registered; the prototype shows the rest of the
 * target MVP. «Настройки» is not an entry: it is shown when a settings entry
 * is visible and opens the first one in this order (BR-19).
 *
 * A later line adds its entry here when it builds the page, with its scope
 * and a landing address under the context prefix of BR-18. mod-002 adds
 * «Организация» → «Общие», «Проекты» and «Проект» → «Общие», «Команды»,
 * «Области» (its design § 2.1, DC-1), so with a real context «Настройки»
 * opens the organization's «Общие». mod-001's «Процесс» (mod-001 D-5,
 * mod-015 D-12) was third in «Организация», and is fourth since mod-016's
 * «Участники» (registered with its page, BR-7). Scoped
 * entries stay hidden until the context is real (D-18), which mod-002's
 * context source makes it, and — mod-016 design § 6.1 — until the organization's
 * served codes are read: every entry's code is a read code all three roles
 * hold, so the three roles see the same entries and the difference is in the
 * controls. mod-003 adds the first
 * work hub, «Рабочие элементы» (design § 4.1; mod-003 OQ-5): project-scoped,
 * so it shows once a real project is chosen, and current on the item's form
 * beneath its landing (mod-003 D-5). mod-006 adds «Доски» and «Бэклоги»
 * after it (its spec § Navigation): team-scoped, so they show once a real
 * team is chosen; each lands on its «Истории» level and its other levels are
 * its page too (`views`), so the switcher moves any of them to the landing.
 * feat-001 renames «Доски» to «Канбан» (BR-H-16; the id and the address
 * segment `boards` stay) and puts the levels in a drop-down (BR-H-15).
 * feat-039 adds the project's own backlog and board, project-level pages of the
 * same two entries (`projectViews`, BR-PB-9): current there like the entry's team
 * pages, and kept, with their level, by a switch of project or organization.
 * feat-040 moves a team's pages to the team's address (BR-TS-1): the two entries need
 * the organization and a team (BR-TS-10), whatever project is chosen. All of it is
 * interim, retired by feat-043 with the switcher it follows.
 */
export const navigationModel: NavigationModel = {
  top: [
    {
      id: 'home',
      label: 'Главная',
      icon: HomeOutlinedIcon,
      scope: 'instance',
      landing: () => HOME_PATH,
    },
  ],
  work: [
    {
      id: 'workitems',
      label: 'Рабочие элементы',
      icon: ListAltOutlinedIcon,
      scope: 'project',
      permission: workItemsPermissions.workItemRead,
      landing: (selection) =>
        selection.organizationId && selection.projectId
          ? workItemsPath(selection.organizationId, selection.projectId)
          : null,
    },
    {
      id: 'boards',
      label: 'Канбан',
      icon: ViewWeekOutlinedIcon,
      scope: 'team',
      permission: boardsPermissions.workItemRead,
      landing: (selection) =>
        selection.organizationId && selection.teamId
          ? boardsLandingPath(selection.organizationId, selection.teamId)
          : null,
      views: (selection) =>
        selection.organizationId && selection.teamId
          ? boardLevelPaths(selection.organizationId, selection.teamId)
          : [],
      projectViews: projectViews(projectBoardLevelOf, projectBoardPath),
    },
    {
      id: 'backlogs',
      label: 'Бэклоги',
      icon: FormatListNumberedIcon,
      scope: 'team',
      permission: boardsPermissions.workItemRead,
      landing: (selection) =>
        selection.organizationId && selection.teamId
          ? backlogsLandingPath(selection.organizationId, selection.teamId)
          : null,
      views: (selection) =>
        selection.organizationId && selection.teamId
          ? backlogLevelPaths(selection.organizationId, selection.teamId)
          : [],
      projectViews: projectViews(projectBacklogLevelOf, projectBacklogPath),
    },
  ],
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
          landing: (selection) =>
            selection.organizationId ? organizationGeneralPath(selection.organizationId) : null,
        },
        {
          id: 'projects',
          label: 'Проекты',
          scope: 'organization',
          permission: organizationsPermissions.projectRead,
          landing: (selection) =>
            selection.organizationId ? projectsPath(selection.organizationId) : null,
        },
        // feat-028 (design § 1): the teams are the organization's. A team's page is beneath this
        // entry's landing; the project group's own «Команды» lists the teams linked to a project.
        {
          id: 'organization-teams',
          label: 'Команды',
          scope: 'organization',
          permission: organizationsPermissions.teamRead,
          landing: (selection) =>
            selection.organizationId ? organizationTeamsPath(selection.organizationId) : null,
        },
        {
          id: 'members',
          label: 'Участники',
          scope: 'organization',
          permission: organizationsPermissions.membershipRead,
          landing: (selection) =>
            selection.organizationId ? membersPath(selection.organizationId) : null,
        },
        {
          id: 'process',
          label: 'Процесс',
          scope: 'organization',
          permission: processPermissions.definitionRead,
          landing: (selection) =>
            selection.organizationId ? processDefinitionPath(selection.organizationId) : null,
        },
      ],
    },
    {
      id: 'project',
      label: 'Проект',
      entries: [
        {
          id: 'project-general',
          label: 'Общие',
          scope: 'project',
          permission: organizationsPermissions.projectRead,
          landing: (selection) =>
            selection.organizationId && selection.projectId
              ? projectGeneralPath(selection.organizationId, selection.projectId)
              : null,
        },
        {
          id: 'teams',
          label: 'Команды',
          scope: 'project',
          permission: organizationsPermissions.teamRead,
          landing: (selection) =>
            selection.organizationId && selection.projectId
              ? teamsPath(selection.organizationId, selection.projectId)
              : null,
        },
        {
          id: 'areas',
          label: 'Области',
          scope: 'project',
          permission: organizationsPermissions.areaRead,
          landing: (selection) =>
            selection.organizationId && selection.projectId
              ? areasPath(selection.organizationId, selection.projectId)
              : null,
        },
      ],
    },
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
        },
      ],
    },
  ],
};
