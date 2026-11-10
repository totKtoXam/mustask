import { useMemo } from 'react';
import { useProjects } from './queries';

/** The caller's effective codes on each active project of the organization, by project id. */
export type ProjectCodes = ReadonlyMap<string, readonly string[]>;

/**
 * The codes a work-item or board surface asks, per project (mod-019 api-contract § 5, BR-SA-18, plan F-6):
 * `GET /projects` carries each project's `codes` once per project, so a board of several projects decides
 * each card by its own project without a request per card. `codes` is `undefined` until the list is read, and
 * a project the list does not hold (deactivated, another organization's) is absent from the map: both fail
 * closed — unknown is not granted.
 */
export function useProjectCodes(
  organizationId: string,
  options: { enabled?: boolean } = {},
): { codes: ProjectCodes | undefined; isPending: boolean } {
  const query = useProjects(organizationId, 'active', options);
  const rows = query.data;
  const codes = useMemo(
    () => (rows === undefined ? undefined : new Map(rows.map((row) => [row.id, row.codes ?? []]))),
    [rows],
  );
  return { codes, isPending: query.isPending && (options.enabled ?? true) };
}
