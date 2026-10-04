import HomeOutlinedIcon from '@mui/icons-material/HomeOutlined';
import { dictionariesPermissions, LANGUAGES_PATH } from '../../features/dictionaries';
import { processDefinitionPath, processPermissions } from '../../features/process';
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
 * and a landing address under the context prefix of BR-18. mod-001's
 * «Процесс» is registered here, in «Организация» (mod-001 D-5, mod-015
 * D-12): its first entry today, third once «Общие» and «Участники» exist.
 * It is organization-scoped, so it stays hidden until the context is real
 * (D-18): before mod-002 nothing visible leads to its page.
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
  work: [],
  settings: [
    {
      id: 'organization',
      label: 'Организация',
      entries: [
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
    { id: 'project', label: 'Проект', entries: [] },
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
