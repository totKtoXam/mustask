import { useMemo } from 'react';
import { useParams } from 'react-router-dom';
import { NotFound } from '../../../shared/ui/StateFrame';
import { isBoardLevel } from '../lib/levels';
import { projectScope } from '../lib/scope';
import { ScopedBoard } from './BoardPage';

/**
 * «Канбан» of a project (feat-039 US-1 head, US-3; design § 5): the team page's
 * board over every item of the project, whatever its team, its cards in the
 * project's order. `:level` is one of the three or the address is unknown
 * (BR-PB-1); the read's own answers validate the rest of the address. Keyed by
 * its project, so another project starts afresh. The head (without a team menu since feat-040
 * BR-TS-11), the columns, a move and the checklists are the team board's; mod-017 lifts BR-PB-6
 * (the first column draws «Новый элемент») and adds the team filter.
 */
export function ProjectBoardPage() {
  const {
    organizationId = '',
    projectId = '',
    level,
  } = useParams<{ organizationId: string; projectId: string; level: string }>();
  // One scope per project: the columns' callbacks depend on it.
  const scope = useMemo(() => projectScope(projectId), [projectId]);
  if (!isBoardLevel(level)) {
    return <NotFound level="page" />;
  }
  return (
    <ScopedBoard
      key={`${organizationId}/${projectId}`}
      organizationId={organizationId}
      scope={scope}
      level={level}
    />
  );
}
