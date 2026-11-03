import type { ComponentType } from 'react';
import { useParams } from 'react-router-dom';
import { GroupingSwitch } from '../components/GroupingSwitch';
import { GroupingSlot } from '../components/groupingSlot';
import { useAddressSelectors, useGroupingChoice } from '../hooks/useAggregateView';
import { isBoardLevel } from '../lib/levels';
import { AggregatePage } from './AggregateViewPages';
import { BacklogPage } from './BacklogPage';
import { BoardPage } from './BoardPage';
import { ProjectBacklogPage } from './ProjectBacklogPage';
import { ProjectBoardPage } from './ProjectBoardPage';

/**
 * A team's or a project's backlog or board with the «Группировка» switch (feat-048 BR-AV-2,
 * BR-AV-3, OQ-7/OQ-8 A). «Без группировки» is the shipped page, untouched, with the switch in its
 * head; any other option is the aggregate view over the same selectors. The default is by project
 * for a team with a department chosen, else none, so a team without one looks as it always did.
 */
function GroupedRoute({
  view,
  title,
  Shipped,
}: {
  view: 'backlogs' | 'boards';
  title: string;
  Shipped: ComponentType;
}) {
  const { organizationId = '', level } = useParams<{ organizationId: string; level: string }>();
  const selectors = useAddressSelectors();
  const choice = useGroupingChoice(organizationId, view, selectors);
  if (choice.grouping === 'none' || !isBoardLevel(level)) {
    return (
      <GroupingSlot.Provider
        value={
          <GroupingSwitch
            offered={choice.offered}
            value={choice.grouping}
            onChange={choice.changeGrouping}
          />
        }
      >
        <Shipped />
      </GroupingSlot.Provider>
    );
  }
  return (
    <AggregatePage
      key={`${view}/${level}`}
      view={view}
      title={title}
      level={level}
      choice={choice}
    />
  );
}

export function TeamBacklogRoute() {
  return <GroupedRoute view="backlogs" title="Бэклог" Shipped={BacklogPage} />;
}

export function TeamBoardRoute() {
  return <GroupedRoute view="boards" title="Канбан" Shipped={BoardPage} />;
}

export function ProjectBacklogRoute() {
  return <GroupedRoute view="backlogs" title="Бэклог" Shipped={ProjectBacklogPage} />;
}

export function ProjectBoardRoute() {
  return <GroupedRoute view="boards" title="Канбан" Shipped={ProjectBoardPage} />;
}
