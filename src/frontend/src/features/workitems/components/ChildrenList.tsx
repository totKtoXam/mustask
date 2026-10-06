import { Link as RouterLink } from 'react-router-dom';
import Link from '@mui/material/Link';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import Typography from '@mui/material/Typography';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type { WorkItemRef } from '../api/workItemsApi';
import { workItemPath } from '../paths';
import { StateName } from './Marks';

/** The list's text when there is nothing in it (feat-001 spec § Screens and copy). */
export const NO_CHILDREN_TEXT = 'Дочерних элементов нет.';

/**
 * «Дочерние элементы» (feat-001 US-3; spec § Screens and copy): the item's
 * direct children as the read answers them — by level, highest first, then by
 * number — each with its type's icon, its identifier as a link to its form,
 * its «Заголовок» and its state. Read-only (BR-H-7): a child is re-parented on
 * its own form or its backlog row.
 */
export function ChildrenList({
  organizationId,
  projectId,
  items,
  labelledBy,
}: {
  organizationId: string;
  projectId: string;
  items: readonly WorkItemRef[];
  /** The id of the heading that names the list. */
  labelledBy: string;
}) {
  if (items.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        {NO_CHILDREN_TEXT}
      </Typography>
    );
  }
  return (
    <List dense disablePadding aria-labelledby={labelledBy}>
      {items.map((child) => (
        <ListItem key={child.id} disableGutters sx={{ gap: 1 }}>
          <TypeMark appearance={child.type.appearance} />
          <Link
            component={RouterLink}
            to={workItemPath(organizationId, projectId, child.identifier)}
            variant="body2"
            sx={{ flexShrink: 0 }}
          >
            {child.identifier}
          </Link>
          <Typography variant="body2" noWrap sx={{ flex: 1, minWidth: 0 }}>
            {child.title}
          </Typography>
          <Typography component="span" variant="body2" sx={{ flexShrink: 0 }}>
            <StateName state={child.state} />
          </Typography>
        </ListItem>
      ))}
    </List>
  );
}
