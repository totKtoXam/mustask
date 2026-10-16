import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Tooltip from '@mui/material/Tooltip';
import { UserAvatar as KitAvatar } from '../../../shared/ui';
import Typography from '@mui/material/Typography';
import PersonOutlineOutlinedIcon from '@mui/icons-material/PersonOutlineOutlined';
import { initials } from '../../../shared/lib/initials';
import { CategoryMarker, TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type {
  WorkItemAssignee,
  WorkItemStateReference,
  WorkItemTypeReference,
} from '../api/workItemsApi';
import { UNASSIGNED_LABEL, assigneeLabel } from '../lib/assignee';

// How a type, a state and an assignee read in the list, the dialog and the
// form (spec § Screens and copy; mod-015 § 2.3): the mark is decoration, the
// text beside it carries the meaning.

const inline = { display: 'inline-flex', alignItems: 'center', gap: 1, minWidth: 0 } as const;

/** «Тип»: the type's icon and its name. */
export function TypeName({ type }: { type: Pick<WorkItemTypeReference, 'appearance' | 'name'> }) {
  return (
    <Box component="span" sx={inline}>
      <TypeMark appearance={type.appearance} />
      {type.name}
    </Box>
  );
}

/** «Статус»: the category dot and the state's name. */
export function StateName({ state }: { state: Pick<WorkItemStateReference, 'name' | 'category'> }) {
  return (
    <Box component="span" sx={inline}>
      <CategoryMarker code={state.category.code} />
      {state.name}
    </Box>
  );
}

/**
 * An initials avatar, 24 px (mod-015 § 10.1), `aria-hidden`: the name beside
 * it is what is read. A name the directory does not know shows a person icon.
 */
export function UserAvatar({ name }: { name: string | null }) {
  const letters = name ? initials(name) : null;
  return (
    <Avatar
      aria-hidden="true"
      sx={(theme) => ({
        width: theme.spacing(3),
        height: theme.spacing(3),
        fontSize: theme.typography.caption.fontSize,
      })}
    >
      {letters ?? <PersonOutlineOutlinedIcon fontSize="inherit" />}
    </Avatar>
  );
}

/**
 * An assignee shown by the avatar alone (feat-032 design § 4.2): a board's
 * checklist row has no name beside it, so the avatar carries it — 24 px,
 * `role="img"`, the name as its accessible name and its tooltip (`assigneeLabel`:
 * the person's name, «Не назначен», «Неизвестный пользователь»). The initials of a
 * known person, the neutral person icon for nobody and for a name the directory
 * does not know. Not a tab stop: the tooltip is for the pointer.
 */
export function AssigneeAvatar({ assignee }: { assignee: WorkItemAssignee | null }) {
  const label = assigneeLabel(assignee);
  return (
    <Tooltip title={label}>
      <Box
        component="span"
        role="img"
        aria-label={label}
        sx={{ display: 'inline-flex', flexShrink: 0 }}
      >
        <KitAvatar name={assignee?.displayName ?? ''} />
      </Box>
    </Tooltip>
  );
}

/** «Исполнитель» in the list: the avatar and the name, or «Не назначен». */
export function AssigneeName({ assignee }: { assignee: WorkItemAssignee | null }) {
  if (assignee === null) {
    return (
      <Typography component="span" variant="body2" color="text.secondary">
        {UNASSIGNED_LABEL}
      </Typography>
    );
  }
  return (
    <Box component="span" sx={inline}>
      <UserAvatar name={assignee.displayName} />
      {assigneeLabel(assignee)}
    </Box>
  );
}
