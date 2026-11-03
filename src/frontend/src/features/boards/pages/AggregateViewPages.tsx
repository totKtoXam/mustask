import { useParams } from 'react-router-dom';
import { NotFound } from '../../../shared/ui/StateFrame';
import { AggregateBacklogView } from '../components/AggregateBacklogView';
import { AggregateBoardView } from '../components/AggregateBoardView';
import { AggregateHeader } from '../components/AggregateHeader';
import { useAddressSelectors, useAggregateView, useNarrowedTag } from '../hooks/useAggregateView';
import { isBoardLevel, type BoardLevelCode } from '../lib/levels';

/**
 * «Бэклог» and «Канбан» above the project level (feat-048 US-10, US-11): the organization's and a
 * department's (`areas/:areaId/…`). The grouping is read from the browser memory, never the address.
 */
function AggregatePage({
  view,
  title,
  level,
}: {
  view: 'backlogs' | 'boards';
  title: string;
  level: BoardLevelCode;
}) {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const selectors = useAddressSelectors();
  const v = useAggregateView({ organizationId, view, level, selectors });
  const narrowed = useNarrowedTag(organizationId, selectors);
  const grouped = v.grouping !== 'none' && (v.groups?.length ?? 0) > 0;
  return (
    <>
      <AggregateHeader
        title={title}
        narrowed={narrowed}
        level={level}
        offered={v.offered}
        grouping={v.grouping}
        onGrouping={v.changeGrouping}
        onCollapseAll={grouped ? v.collapseAll : undefined}
      />
      {view === 'backlogs' ? <AggregateBacklogView v={v} /> : <AggregateBoardView v={v} />}
    </>
  );
}

function LevelGuard({ view, title }: { view: 'backlogs' | 'boards'; title: string }) {
  const { level } = useParams<{ level: string }>();
  if (!isBoardLevel(level)) return <NotFound level="page" />;
  return <AggregatePage key={`${view}/${level}`} view={view} title={title} level={level} />;
}

export function AggregateBacklogPage() {
  return <LevelGuard view="backlogs" title="Бэклог" />;
}

export function AggregateBoardPage() {
  return <LevelGuard view="boards" title="Канбан" />;
}
