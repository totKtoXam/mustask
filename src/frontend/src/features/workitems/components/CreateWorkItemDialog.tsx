import { useEffect, useId, useRef, useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import MenuItem from '@mui/material/MenuItem';
import Skeleton from '@mui/material/Skeleton';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useAreaTree, useTeams } from '../../organizations';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { FieldsSkeleton, InlineNotice } from '../../../shared/ui';
import { FormDialog } from '../../../shared/ui/FormDialog';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type { CreateWorkItemBody, WorkItem, WorkItemTeam } from '../api/workItemsApi';
import type { CreatableType } from '../lib/process';
import { placeRefusal } from '../lib/refusals';
import { newRequestId } from '../lib/requestId';
import { titleError } from '../lib/rules';
import { backlogCaption, defaultTeamId } from '../lib/teams';
import { TypeName } from './Marks';
import { TeamField } from './TeamField';

/**
 * «Новый рабочий элемент» (mod-003 US-1; mod-017 US-2, design § 4): «Тип» preset by the type
 * picker, «Команда» and «Заголовок», checked on submit; nothing is sent while the title is
 * refused. The text names the type’s initial state and the project’s root area, read from
 * mod-002’s tree (api-contract § 3) — on every surface, since an item’s area is the root now
 * (BR-OT-10).
 *
 * «Команда» starts with the team of the backlog or board the dialog was opened on
 * (`startTeamId`), else the project’s default team, else «Без команды» (BR-OT-3), decided when
 * the teams read ends — the dialog reads them afresh at every opening — and never re-set while
 * open: it sends what it shows, `team: { teamId }`, `null` included. While the teams are read
 * «Создать» is unavailable and «Заголовок» takes typing; a failed read says so with
 * «Повторить». A refusal of the team (not found, not in the project) lands under «Команда» with
 * the choice kept, the focus on it and the teams read again; the next pick clears it.
 *
 * One submission is one `requestId` (BR-M3-26): it is made at the first «Создать» and kept for
 * every retry of that submission — a double activation, a resend after a lost answer or a
 * refusal — so the server creates at most one item; the dialog is unmounted on close and after
 * success, so the next one starts afresh. A refusal lands on «Заголовок» or in the `Alert` by
 * its code, and the input is kept.
 */
export function CreateWorkItemDialog({
  organizationId,
  projectId,
  types,
  initialTypeId,
  startTeamId,
  submit,
  onCreated,
  onClose,
}: {
  organizationId: string;
  projectId: string;
  types: readonly CreatableType[];
  initialTypeId: string;
  /** A team’s backlog or board: that team starts the dialog. Absent: the project’s default team. */
  startTeamId?: string;
  submit(body: CreateWorkItemBody): Promise<WorkItem>;
  onCreated(item: WorkItem): void;
  /** `contextGone`: the project or organization answered not found meanwhile. */
  onClose(contextGone: boolean): void;
}) {
  const tree = useAreaTree(organizationId, projectId);
  const teams = useTeams(organizationId, projectId, { fresh: true });
  const [typeId, setTypeId] = useState(initialTypeId);
  const [title, setTitle] = useState('');
  const [titleRefusal, setTitleRefusal] = useState<string | null>(null);
  // `undefined`: not decided yet (the teams are being read). `null`: «Без команды».
  const [team, setTeam] = useState<WorkItemTeam | null | undefined>(undefined);
  const [teamRefusal, setTeamRefusal] = useState<string | undefined>(undefined);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const requestId = useRef<string | null>(null);
  const inFlight = useRef(false);
  const teamInput = useRef<HTMLInputElement>(null);
  const captionId = useId();

  // The starting value is decided once, when the first read of this opening ends.
  const teamsReady = teams.isSuccess && !teams.isFetching;
  if (teamsReady && team === undefined) {
    const known =
      startTeamId === undefined ? undefined : teams.data.find((t) => t.id === startTeamId);
    const startId = known ? known.id : defaultTeamId(teams.data);
    const start = teams.data.find((candidate) => candidate.id === startId);
    setTeam(start ? { id: start.id, name: start.name } : null);
  }

  useEffect(() => {
    if (teamRefusal !== undefined) {
      teamInput.current?.focus();
    }
  }, [teamRefusal]);

  async function handleSubmit() {
    if (inFlight.current || team === undefined) {
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
      onCreated(
        await submit({
          requestId: requestId.current,
          typeId,
          title,
          team: { teamId: team?.id ?? null },
        }),
      );
    } catch (error) {
      const placed = placeRefusal(error, ['title', 'team']);
      setTitleRefusal(placed.fields.title ?? null);
      setTeamRefusal(placed.fields.team);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.frame !== null);
      setBusy(false);
      if (placed.fields.team !== undefined) {
        void teams.refetch();
      }
    } finally {
      inFlight.current = false;
    }
  }

  const chosenType = types.find((type) => type.id === typeId);
  const stateName = chosenType?.initialStateName ?? '';
  const root = tree.data?.find((node) => node.parentId === null);
  const treeLoading = tree.isPending || (tree.isError && tree.isFetching);
  const teamsFailed = teams.isError && !teams.isFetching;

  return (
    <FormDialog
      open
      title="Новый рабочий элемент"
      submitLabel="Создать"
      busy={busy}
      error={alert}
      submitDisabled={team === undefined}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      {treeLoading ? (
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
      {teamsFailed ? (
        <InlineNotice
          severity="error"
          action={{ label: 'Повторить', onClick: () => void teams.refetch() }}
        >
          {apiErrorText(teams.error)}
        </InlineNotice>
      ) : team === undefined ? (
        <FieldsSkeleton count={1} />
      ) : (
        <>
          <TeamField
            value={team}
            teams={teams.data}
            labelPlacement="top"
            frame="always"
            onChange={(next) => {
              setTeam(next);
              setTeamRefusal(undefined);
            }}
            error={teamRefusal}
            busy={busy}
            inputRef={teamInput}
            describedBy={chosenType?.onBacklog ? captionId : undefined}
          />
          {chosenType?.onBacklog && (
            <Typography id={captionId} variant="caption" color="text.secondary">
              {backlogCaption(team?.name ?? null)}
            </Typography>
          )}
        </>
      )}
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
