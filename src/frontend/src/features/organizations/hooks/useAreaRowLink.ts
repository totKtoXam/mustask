import { useCallback } from 'react';
import type { AreaNode } from '../api/organizationsApi';
import { organizationAreaPath } from '../paths';
import { useObjectLink, type ObjectLink } from './useObjectLink';

/**
 * Where a node of «Структура» leads, shared by the tree and the diagram (feat-049 BR-SD-6): a
 * department and a project open their own page (a project its «Общие», OQ-1 A); a sub-area opens
 * its page under its project's menu; a deactivated project's node and sub-areas open nothing
 * (OQ-3 A).
 */
export function useAreaRowLink(organizationId: string) {
  const objectLink = useObjectLink(organizationId);
  return useCallback(
    (area: Pick<AreaNode, 'id' | 'type' | 'projectId' | 'projectActive'>): ObjectLink | null => {
      if (area.projectActive === false) return null;
      if (area.type === 'department') return objectLink('department', area.id);
      if (area.type === 'project' && area.projectId) return objectLink('project', area.projectId);
      // A sub-area keeps the team that fits its project (D-6).
      if (area.projectId) return objectLink('project', area.projectId, area.id);
      return { to: organizationAreaPath(organizationId, area.id) };
    },
    [objectLink, organizationId],
  );
}
