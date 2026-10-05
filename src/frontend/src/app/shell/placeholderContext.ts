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
 * The switcher's data before mod-002 (US-5, BR-11): the fixed placeholder
 * dataset. Ids follow the prototype's. The product no longer mounts it —
 * `AppLayout` reads mod-002's real lists (its D-o) — and it stays as the
 * shell tests' static source: `renderShell` hands it to the shell when a
 * test passes none (mod-002 plan-frontend § 2.4).
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

/** The placeholder dataset as a source (the shell's before mod-002, design § 5.7). */
export function usePlaceholderContextSource(): WorkContextSource {
  return useStaticContextSource(PLACEHOLDER_ORGANIZATIONS);
}
