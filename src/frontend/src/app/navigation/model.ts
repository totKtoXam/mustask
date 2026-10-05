import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined';
import ListAltOutlinedIcon from '@mui/icons-material/ListAltOutlined';
import { dictionariesPermissions, LANGUAGES_PATH } from '../../features/dictionaries';
import {
  areasPath,
  organizationGeneralPath,
  organizationsPermissions,
  projectGeneralPath,
  projectsPath,
  teamsPath,
} from '../../features/organizations';
import { processDefinitionPath, processPermissions } from '../../features/process';
import { workItemsPath, workItemsPermissions } from '../../features/workitems';
import { HOME_PATH } from '../../shared/config/paths';
import type { NavigationModel } from './types';

/** The label of the derived «Настройки» entry and of the settings navigation's title (BR-19). */
export const SETTINGS_LABEL = 'Настройки';

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
 * mod-015 D-12) is third in «Организация» now, fourth once mod-016's
 * «Участники» exists. Scoped entries stay hidden until the context is real
 * (D-18), which mod-002's context source makes it. mod-003 adds the first
 * work hub, «Рабочие элементы» (design § 4.1; mod-003 OQ-5): project-scoped,
 * so it shows once a real project is chosen, and current on the item's form
 * beneath its landing (mod-003 D-5).
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
  ],
};
