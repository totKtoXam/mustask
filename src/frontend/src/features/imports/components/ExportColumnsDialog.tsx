import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import Typography from '@mui/material/Typography';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import {
  ActionButton,
  DialogShell,
  FieldsSkeleton,
  InlineNotice,
  StateFrame,
} from '../../../shared/ui';
import { useTemplateColumns } from '../hooks/queries';
import { texts } from '../lib/texts';

/** S2c — the export header texts of a template, in order, for a member who may only read (design § 2). */
export function ExportColumnsDialog({
  organizationId,
  template,
  onClose,
}: {
  organizationId: string;
  template: { id: string; name: string };
  onClose: () => void;
}) {
  const query = useTemplateColumns(organizationId, template.id, true);
  const columns = query.data?.columns ?? [];

  return (
    <DialogShell
      open
      size="form"
      title={texts.columnsTitle(template.name)}
      onClose={onClose}
      footer={<ActionButton label={texts.close} onClick={onClose} />}
    >
      {query.isPending ? (
        <FieldsSkeleton count={3} />
      ) : query.isError ? (
        <InlineNotice
          severity="error"
          action={{ label: texts.retry, onClick: () => void query.refetch() }}
        >
          {apiErrorText(query.error)}
        </InlineNotice>
      ) : columns.length === 0 ? (
        <StateFrame compact icon={InboxOutlinedIcon} heading={texts.columnsEmpty} />
      ) : (
        <>
          <List dense>
            {columns.map((column) => (
              <ListItem key={column}>
                <ListItemText primary={column} />
              </ListItem>
            ))}
          </List>
          <Typography variant="caption" sx={{ color: 'text.secondary' }}>
            {texts.columnsCaption}
          </Typography>
        </>
      )}
    </DialogShell>
  );
}
