import { memo } from 'react';
import type { MouseEvent } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import { InlineEmpty, StateLabel, TruncatedText, TypeMark } from '../../../../shared/ui';
import type { WorkItemRef } from '../../api/workItemsApi';
import { cardTexts } from '../../lib/cardTexts';
import { isPlainClick } from '../../lib/plainClick';
import { workItemPath } from '../../paths';

/**
 * «Дочерние элементы» (feat-001 US-3; spec § Screens and copy): the item's
 * direct children as the read answers them — by level, highest first, then by
 * number — each with its type's icon, its identifier as a link to its page, its
 * «Заголовок» and its state. Read-only (BR-H-7): a child is re-parented on its
 * own card or its backlog row. In the dialog a plain click on an identifier
 * switches the dialog to that child (BR-C-5); on the page the link navigates.
 */
function ChildItemsView({
  organizationId,
  projectId,
  items,
  labelledBy,
  onOpenItem,
}: {
  organizationId: string;
  projectId: string;
  items: readonly WorkItemRef[];
  /** The id of the head that names the list. */
  labelledBy: string;
  onOpenItem?: (identifier: string) => void;
}) {
  if (items.length === 0) {
    return <InlineEmpty>{cardTexts.noChildren}</InlineEmpty>;
  }
  return (
    <List dense disablePadding aria-labelledby={labelledBy}>
      {items.map((child) => (
        <ListItem key={child.id} disableGutters sx={{ gap: 1 }}>
          <TypeMark appearance={child.type.appearance} compact />
          <Link
            component={RouterLink}
            to={workItemPath(organizationId, projectId, child.identifier)}
            variant="body2"
            sx={{ flexShrink: 0 }}
            onClick={(event: MouseEvent<HTMLAnchorElement>) => {
              if (onOpenItem && isPlainClick(event)) {
                event.preventDefault();
                onOpenItem(child.identifier);
              }
            }}
          >
            {child.identifier}
          </Link>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <TruncatedText>{child.title}</TruncatedText>
          </Box>
          <Box sx={{ minWidth: 0, flexShrink: 1 }}>
            <StateLabel categoryCode={child.state.category.code} name={child.state.name} clip />
          </Box>
        </ListItem>
      ))}
    </List>
  );
}

export const ChildItems = memo(ChildItemsView);
