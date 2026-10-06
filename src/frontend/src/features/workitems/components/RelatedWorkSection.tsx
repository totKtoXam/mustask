import { useId } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import Typography from '@mui/material/Typography';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import type { WorkItemRef } from '../api/workItemsApi';
import { workItemPath } from '../paths';
import { ChildrenList } from './ChildrenList';
import { StateName } from './Marks';
import { ParentPicker } from './ParentPicker';

/**
 * The form's section «Связанная работа» (feat-001 US-1, US-3; spec § Screens
 * and copy; mod-015 § 10.3), below «Описание»: «Родитель» — the draft's value
 * with its state beside it, and «Открыть <ID>» to the stored parent's form
 * while one is stored — then the read-only «Дочерние элементы (<n>)».
 */
export function RelatedWorkSection({
  organizationId,
  projectId,
  identifier,
  stored,
  childItems,
  value,
  disabled,
  error,
  helperText,
  onChange,
}: {
  organizationId: string;
  projectId: string;
  identifier: string;
  /** The parent as loaded or last saved: what «Открыть» opens. */
  stored: WorkItemRef | null;
  childItems: readonly WorkItemRef[];
  /** The draft's parent: what the field shows. */
  value: WorkItemRef | null;
  disabled: boolean;
  error: string | undefined;
  helperText: string | null;
  onChange(parent: WorkItemRef | null): void;
}) {
  const headingId = useId();
  const childrenHeadingId = useId();
  return (
    <Box component="section" aria-labelledby={headingId} sx={{ mt: 3 }}>
      <Typography
        id={headingId}
        variant="h2"
        component="h2"
        sx={{ pb: 1, mb: 2, borderBottom: 1, borderColor: 'divider' }}
      >
        Связанная работа
      </Typography>
      <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 1, flexWrap: 'wrap' }}>
        <ParentPicker
          organizationId={organizationId}
          projectId={projectId}
          identifier={identifier}
          value={value}
          disabled={disabled}
          error={error}
          helperText={helperText}
          onChange={onChange}
        />
        <Box
          sx={(theme) => ({
            display: 'flex',
            alignItems: 'center',
            gap: 1,
            minHeight: theme.spacing(5),
          })}
        >
          {value !== null && (
            <Typography component="span" variant="body2" color="text.secondary">
              <StateName state={value.state} />
            </Typography>
          )}
          {stored !== null && (
            <IconButton
              component={RouterLink}
              to={workItemPath(organizationId, projectId, stored.identifier)}
              aria-label={`Открыть ${stored.identifier}`}
              size="small"
            >
              <OpenInNewIcon fontSize="small" />
            </IconButton>
          )}
        </Box>
      </Box>
      <Typography id={childrenHeadingId} variant="subtitle2" component="h3" sx={{ mt: 3, mb: 1 }}>
        {`Дочерние элементы (${childItems.length})`}
      </Typography>
      <ChildrenList
        organizationId={organizationId}
        projectId={projectId}
        items={childItems}
        labelledBy={childrenHeadingId}
      />
    </Box>
  );
}
