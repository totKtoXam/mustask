import { useMemo } from 'react';
import { useParams } from 'react-router-dom';
import { NotFound } from '../../../shared/ui/StateFrame';
import { isBoardLevel } from '../lib/levels';
import { projectScope } from '../lib/scope';
import { ScopedBacklog } from './BacklogPage';

/**
 * «Бэклоги» of a project (feat-039 US-1, US-2; design § 4): the team page's
 * backlog over every item of the project, whatever its team, in the project's own
 * order. `:level` is one of the three or the address is unknown (BR-PB-1); the
 * read's own answers validate the rest of the address — the organization, the
 * project (a team frame cannot occur) and a level the process lacks. Keyed by its
 * project, so another project starts afresh, while the level drop-down keeps the
 * head. The head (without a team menu since feat-040 BR-TS-11), the table, the row actions and
 * the card over the page are the team backlog's; mod-017 lifts BR-PB-6 (the page creates too) and
 * adds the team filter.
 */
export function ProjectBacklogPage() {
  const {
    organizationId = '',
    projectId = '',
    level,
  } = useParams<{ organizationId: string; projectId: string; level: string }>();
  // One scope per project: the table's callbacks depend on it.
  const scope = useMemo(() => projectScope(projectId), [projectId]);
  if (!isBoardLevel(level)) {
    return <NotFound level="page" />;
  }
  return (
    <ScopedBacklog
      key={`${organizationId}/${projectId}`}
      organizationId={organizationId}
      scope={scope}
      level={level}
    />
  );
}
