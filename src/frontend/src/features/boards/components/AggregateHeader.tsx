import { useLocation } from 'react-router-dom';
import Box from '@mui/material/Box';
import UnfoldLessIcon from '@mui/icons-material/UnfoldLess';
import { PageHeader, Tag } from '../../../shared/ui';
import type { Command } from '../../../shared/ui/PageHeader';
import type { Grouping } from '../api/aggregateApi';
import { aggregateTexts } from '../lib/aggregateTexts';
import type { BoardLevelCode } from '../lib/levels';
import { GroupingSwitch } from './GroupingSwitch';
import { LevelMenu } from './LevelMenu';

/**
 * The head of an aggregate view (feat-048 design § 2): the view's name, the «сужено» tag, the
 * tab, «Свернуть все» in the bar, and at the end of the tab row the level drop-down (backlog and
 * board) and «Группировка». Only one head, so the three views share it.
 */
export function AggregateHeader({
  title,
  narrowed,
  level,
  offered,
  grouping,
  onGrouping,
  onCollapseAll,
  primaryAction,
}: {
  title: string;
  narrowed?: string;
  /** Absent on «Рабочие элементы»: the list has no level drop-down. */
  level?: BoardLevelCode;
  offered: readonly Grouping[];
  grouping: Grouping;
  onGrouping(next: Grouping): void;
  /** Absent when nothing is grouped. */
  onCollapseAll?: () => void;
  /** The board's «Новый рабочий элемент» (feat-060); absent on the other views. */
  primaryAction?: Command;
}) {
  const { pathname, search } = useLocation();
  return (
    <PageHeader
      variant="team"
      title={title}
      documentTitle={title}
      titleControl={narrowed ? <Tag label={narrowed} /> : undefined}
      primaryAction={primaryAction}
      commands={
        onCollapseAll
          ? [
              {
                label: aggregateTexts.collapseAll,
                icon: <UnfoldLessIcon />,
                onClick: onCollapseAll,
              },
            ]
          : []
      }
      views={[{ label: title, to: `${pathname}${search}` }]}
      viewsLabel={title}
      viewsEnd={
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
          <GroupingSwitch offered={offered} value={grouping} onChange={onGrouping} />
          {level && (
            <LevelMenu
              level={level}
              pathOf={(next) =>
                `${pathname.replace(/\/(backlogs|boards)\/[^/]+$/, `/$1/${next}`)}${search}`
              }
            />
          )}
        </Box>
      }
    />
  );
}
