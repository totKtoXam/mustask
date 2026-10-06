import { useRef, useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import MenuItem from '@mui/material/MenuItem';
import Skeleton from '@mui/material/Skeleton';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useAreaTree } from '../../organizations';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { FormDialog } from '../../../shared/ui/FormDialog';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type { CreateWorkItemBody, WorkItem } from '../api/workItemsApi';
import type { CreatableType } from '../lib/process';
import { placeRefusal } from '../lib/refusals';
import { newRequestId } from '../lib/requestId';
import { titleError } from '../lib/rules';
import { TypeName } from './Marks';

/**
 * The team a dialog opened on a team's backlog or board creates for (mod-006
 * spec § Dialog): its name and its default area's path as the surface read
 * them, which the text quotes. The item itself takes the default as it stands
 * when it is created (mod-006 api-contract § 7): the caller sends `teamId`.
 */
export interface CreateDialogTeam {
  name: string;
  defaultAreaPath: string;
}

/**
 * «Новый рабочий элемент» (US-1; spec § Dialog; mod-015 § 9.5): «Тип» preset
 * by the type picker and «Заголовок», checked on submit; nothing is sent while
 * the title is refused. The text names the type's initial state and the
 * project's root area, read from mod-002's tree (api-contract § 3) — or, given
 * a `team` (mod-006 spec § Dialog), that team's default area and backlog, with
 * no tree read; «Тип» then offers the level's types the caller passes.
 *
 * One submission is one `requestId` (BR-M3-26): it is made at the first
 * «Создать» and kept for every retry of that submission — a double activation,
 * a resend after a lost answer or a refusal — so the server creates at most
 * one item; the dialog is unmounted on close and after success, so the next
 * one starts afresh. A refusal lands on «Заголовок» or in the `Alert` by its
 * code, and the input is kept.
 */
export function CreateWorkItemDialog({
  organizationId,
  projectId,
  types,
  initialTypeId,
  team,
  submit,
  onCreated,
  onClose,
}: {
  organizationId: string;
  projectId: string;
  types: readonly CreatableType[];
  initialTypeId: string;
  /** Absent: the project's list, the root area. */
  team?: CreateDialogTeam;
  submit(body: CreateWorkItemBody): Promise<WorkItem>;
  onCreated(item: WorkItem): void;
  /** `contextGone`: the team, project or organization answered not found meanwhile. */
  onClose(contextGone: boolean): void;
}) {
  const tree = useAreaTree(organizationId, projectId, { enabled: team === undefined });
  const [typeId, setTypeId] = useState(initialTypeId);
  const [title, setTitle] = useState('');
  const [titleRefusal, setTitleRefusal] = useState<string | null>(null);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const requestId = useRef<string | null>(null);
  const inFlight = useRef(false);

  async function handleSubmit() {
    if (inFlight.current) {
      return;
    }
    const refused = titleError(title);
    if (refused) {
      setTitleRefusal(refused);
      return;
    }
    requestId.current ??= newRequestId();
    inFlight.current = true;
    setBusy(true);
    setAlert(null);
    try {
      onCreated(await submit({ requestId: requestId.current, typeId, title }));
    } catch (error) {
      const placed = placeRefusal(error, ['title']);
      setTitleRefusal(placed.fields.title ?? null);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.frame !== null);
      setBusy(false);
    } finally {
      inFlight.current = false;
    }
  }

  const stateName = types.find((type) => type.id === typeId)?.initialStateName ?? '';
  const root = tree.data?.find((node) => node.parentId === null);
  const treeLoading = tree.isPending || (tree.isError && tree.isFetching);

  return (
    <FormDialog
      open
      title="Новый рабочий элемент"
      submitLabel="Создать"
      busy={busy}
      error={alert}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      {team ? (
        <Typography variant="body2" color="text.secondary">
          {`Рабочий элемент будет создан в статусе «${stateName}» в области «${team.defaultAreaPath}» и встанет в конец бэклога команды «${team.name}». Остальные поля заполняются в его форме.`}
        </Typography>
      ) : treeLoading ? (
        <Skeleton variant="text" aria-hidden="true" />
      ) : tree.isError ? (
        <Alert
          severity="error"
          action={
            <Button color="inherit" size="small" onClick={() => void tree.refetch()}>
              Повторить
            </Button>
          }
        >
          {apiErrorText(tree.error)}
        </Alert>
      ) : (
        <Typography variant="body2" color="text.secondary">
          {`Рабочий элемент будет создан в статусе «${stateName}» в области «${root?.path ?? ''}». Остальные поля заполняются в его форме.`}
        </Typography>
      )}
      <TextField
        select
        label="Тип"
        value={typeId}
        disabled={busy}
        onChange={(event) => setTypeId(event.target.value)}
        slotProps={{
          select: {
            renderValue: (value) => {
              const type = types.find((candidate) => candidate.id === value);
              return type ? <TypeName type={type} /> : null;
            },
          },
        }}
      >
        {types.map((type) => (
          <MenuItem key={type.id} value={type.id}>
            <ListItemIcon>
              <TypeMark appearance={type.appearance} />
            </ListItemIcon>
            <ListItemText>{type.name}</ListItemText>
          </MenuItem>
        ))}
      </TextField>
      <TextField
        label="Заголовок"
        autoFocus
        value={title}
        disabled={busy}
        error={titleRefusal !== null}
        helperText={titleRefusal ?? 'До 255 символов'}
        onChange={(event) => {
          setTitle(event.target.value);
          setTitleRefusal(null);
        }}
        slotProps={{
          inputLabel: { required: true },
          htmlInput: { 'aria-required': true, 'aria-invalid': titleRefusal !== null },
        }}
      />
    </FormDialog>
  );
}
