import { useCallback, useMemo, useState } from 'react';
import {
  firstSelection,
  selectInWorkContext,
  type WorkContextLevel,
  type WorkContextOrganization,
  type WorkContextSelection,
  type WorkContextSource,
} from '../../shared/lib/workContext';

/**
 * The switcher data registry until mod-002 (US-5, BR-11): the fixed
 * placeholder dataset, shipped with the panel. Ids follow the prototype's.
 */
export const PLACEHOLDER_ORGANIZATIONS: readonly WorkContextOrganization[] = [
  {
    id: 'org-1',
    name: 'Пример организации 1',
    projects: [
      {
        id: 'prj-1',
        name: 'Пример проекта 1',
        teams: [
          { id: 'team-1', name: 'Пример команды 1' },
          { id: 'team-2', name: 'Пример команды 2' },
        ],
      },
      {
        id: 'prj-2',
        name: 'Пример проекта 2',
        teams: [{ id: 'team-3', name: 'Пример команды 3' }],
      },
    ],
  },
  {
    id: 'org-2',
    name: 'Пример организации 2',
    projects: [
      {
        id: 'prj-3',
        name: 'Пример проекта 3',
        teams: [{ id: 'team-4', name: 'Пример команды 4' }],
      },
    ],
  },
];

/**
 * A context source over fixed lists: the selection lives in component state,
 * so a reload starts from the first entries again, and nothing is fetched or
 * stored (BR-11). Never real: scoped navigation entries stay hidden (D-18).
 */
export function useStaticContextSource(
  organizations: readonly WorkContextOrganization[],
): WorkContextSource {
  const [selection, setSelection] = useState<WorkContextSelection>(() =>
    firstSelection(organizations),
  );

  const select = useCallback(
    (level: WorkContextLevel, id: string) =>
      setSelection((current) => selectInWorkContext(organizations, current, level, id)),
    [organizations],
  );

  return useMemo<WorkContextSource>(
    () => ({ status: 'ready', organizations, selection, isReal: false, select }),
    [organizations, selection, select],
  );
}

/** The shell's source until mod-002 replaces it (design § 5.7). */
export function usePlaceholderContextSource(): WorkContextSource {
  return useStaticContextSource(PLACEHOLDER_ORGANIZATIONS);
}
