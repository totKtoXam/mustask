import { useEffect, useId, useRef, useState } from 'react';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import MenuItem from '@mui/material/MenuItem';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useTeamProjects, useTeams, type TeamProject } from '../../organizations';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { FieldsSkeleton, InlineNotice, InlinePickerField, LiveRegion } from '../../../shared/ui';
import { FormDialog } from '../../../shared/ui/FormDialog';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type { CreateWorkItemBody, WorkItem, WorkItemTeam } from '../api/workItemsApi';
import type { CreatableType } from '../lib/process';
import { projectTexts } from '../lib/projects';
import { placeRefusal } from '../lib/refusals';
import { newRequestId } from '../lib/requestId';
import { titleError } from '../lib/rules';
import {
  backlogCaption,
  defaultTeamId,
  holdingCaption,
  createdInStateLine,
  teamDroppedLine,
} from '../lib/teams';
import { TypeName } from './Marks';
import { TeamField } from './TeamField';

/**
 * Where the dialog creates (feat-040 BR-TS-9): in the page's project, as the project's own
 * backlog and board do (mod-017 BR-OT-3), or — on a team's backlog or board, which holds the
 * items of several projects — in a project chosen in the dialog, among the team's linked active
 * projects. `filterProjectIds` is the view's project filter, which preselects a single project.
 */
export type CreatePlace =
  | { kind: 'project'; projectId: string }
  | {
      kind: 'team';
      /** The view's team: «Команда» starts as it, and holds it until a project is chosen (D-4). */
      team: { id: string; name: string };
      filterProjectIds: readonly string[];
    };

/**
 * «Новый рабочий элемент» (mod-003 US-1; mod-017 US-2, design § 4; feat-040 US-5, design § 6): «Тип»
 * preset by the type picker, «Команда» and «Заголовок», checked on submit; nothing is sent while the
 * title is refused. The text names the type’s initial state only (mod-018 design § 8: the dialog reads no area tree).
 *
 * On a project’s views «Команда» starts with the project’s default team (BR-OT-3), decided when the
 * teams read ends — the dialog reads them afresh at every opening — and never re-set while open:
 * it sends what it shows, `team: { teamId }`, `null` included. While the teams are read «Создать»
 * is unavailable and «Заголовок» takes typing; a failed read says so with «Повторить». A refusal of
 * the team (not found, not in the project) lands under «Команда» with the choice kept, the focus on
 * it and the teams read again; the next pick clears it.
 *
 * On a team’s views «Проект» comes first: a search among the team’s linked active projects, read
 * afresh at every opening, required, and preselected — decided once, when that read ends — when the
 * view’s project filter names exactly one of them or the team has exactly one. «Команда» starts as
 * the view’s team and, until a project is chosen, is read-only and holds it (D-4); once a project is
 * chosen it offers that project’s linked teams and «Без команды», and a project the team shown is
 * not linked to sets it back to the view’s team, announced politely, with a line in the caption’s
 * place until the next pick (D-5). A refusal of the project lands under «Проект», a refusal of the
 * team under «Команда»; neither closes the dialog.
 *
 * One submission is one `requestId` (BR-M3-26): it is made at the first «Создать» and kept for
 * every retry of that submission — a double activation, a resend after a lost answer or a
 * refusal — so the server creates at most one item; a different project is a different request,
 * and starts a new one. The dialog is unmounted on close and after success, so the next one starts
 * afresh. A refusal lands on «Заголовок» or in the `Alert` by its code, and the input is kept.
 */
export function CreateWorkItemDialog({
  organizationId,
  place,
  types,
  initialTypeId,
  submit,
  onCreated,
  onClose,
}: {
  organizationId: string;
  place: CreatePlace;
  types: readonly CreatableType[];
  initialTypeId: string;
  submit(projectId: string, body: CreateWorkItemBody): Promise<WorkItem>;
  onCreated(item: WorkItem): void;
  /** `contextGone`: the project or organization answered not found meanwhile. */
  onClose(contextGone: boolean): void;
}) {
  const teamPlace = place.kind === 'team' ? place : null;
  const viewTeam = teamPlace?.team;
  const projects = useTeamProjects(organizationId, viewTeam?.id ?? '', {
    enabled: teamPlace !== null,
    fresh: true,
  });
  // A team's view: `undefined` — the projects are being read, `null` — none chosen.
  const [project, setProject] = useState<TeamProject | null | undefined>(undefined);
  const projectId = place.kind === 'project' ? place.projectId : project?.id;
  const teams = useTeams(organizationId, projectId ?? '', {
    enabled: projectId !== undefined,
    fresh: true,
  });
  const [typeId, setTypeId] = useState(initialTypeId);
  const [title, setTitle] = useState('');
  const [titleRefusal, setTitleRefusal] = useState<string | null>(null);
  const [projectRefusal, setProjectRefusal] = useState<string | undefined>(undefined);
  // The project's teams: `undefined` — not decided yet. `null` — «Без команды». A team’s view
  // starts as the view’s team, which needs no read.
  const [team, setTeam] = useState<WorkItemTeam | null | undefined>(
    viewTeam ? { id: viewTeam.id, name: viewTeam.name } : undefined,
  );
  const [teamRefusal, setTeamRefusal] = useState<string | undefined>(undefined);
  // D-5: the line that replaces the caption after a project dropped the team, and how many times
  // one has been said (each is announced, a repeat too).
  const [teamDropped, setTeamDropped] = useState<string | null>(null);
  const [droppedSerial, setDroppedSerial] = useState(0);
  const [checkedFor, setCheckedFor] = useState<string | null>(null);
  const focusedOnce = useRef(false);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const requestId = useRef<string | null>(null);
  const inFlight = useRef(false);
  const projectInput = useRef<HTMLInputElement>(null);
  const teamInput = useRef<HTMLInputElement>(null);
  const titleInput = useRef<HTMLInputElement>(null);
  const captionId = useId();

  // A team’s view: the project is decided once, when the first read of this opening ends — the
  // view’s filter names exactly one of the team’s projects, else the team has exactly one, else none.
  const projectsReady = projects.isSuccess && !projects.isFetching;
  if (teamPlace && projectsReady && project === undefined) {
    const offered = projects.data;
    const filtered = offered.filter((candidate) =>
      teamPlace.filterProjectIds.some((id) => id.toLowerCase() === candidate.id.toLowerCase()),
    );
    const preselected =
      filtered.length === 1 ? filtered[0] : offered.length === 1 ? offered[0] : undefined;
    setProject(preselected ?? null);
  }

  // A project’s own views: the starting team is decided once, when the first read ends.
  const teamsReady = projectId !== undefined && teams.isSuccess && !teams.isFetching;
  if (place.kind === 'project' && teamsReady && team === undefined) {
    const start = teams.data.find((candidate) => candidate.id === defaultTeamId(teams.data));
    setTeam(start ? { id: start.id, name: start.name } : null);
  }

  // A team’s view: the teams of the project chosen decide whether the team shown stays; once per
  // project. The view’s team is linked to every project offered, so it always stands again.
  if (teamPlace && project && teamsReady && checkedFor !== project.id) {
    setCheckedFor(project.id);
    if (team && !teams.data.some((candidate) => candidate.id === team.id)) {
      setTeamDropped(teamDroppedLine(team.name, project.name, teamPlace.team.name));
      setDroppedSerial((count) => count + 1);
      setTeam({ id: teamPlace.team.id, name: teamPlace.team.name });
    }
  }

  // Focus: «Проект» when none is chosen, else «Заголовок» (design § 6.2). The title takes it on
  // opening a project’s dialog (autoFocus); a team’s waits for the decision, once.
  const decided = teamPlace !== null && project !== undefined;
  useEffect(() => {
    if (decided && !focusedOnce.current) {
      focusedOnce.current = true;
      (project === null ? projectInput : titleInput).current?.focus();
    }
  }, [decided, project]);

  useEffect(() => {
    if (teamRefusal !== undefined) {
      teamInput.current?.focus();
    }
  }, [teamRefusal]);

  useEffect(() => {
    if (projectRefusal !== undefined) {
      projectInput.current?.focus();
    }
  }, [projectRefusal]);

  function chooseProject(next: TeamProject | null) {
    if (next === null || next.id === project?.id) {
      return;
    }
    setProject(next);
    setProjectRefusal(undefined);
    setTeamRefusal(undefined);
    setTeamDropped(null);
    // Another project is another request (BR-M3-26): the next «Создать» makes its own.
    requestId.current = null;
  }

  async function handleSubmit() {
    if (inFlight.current || team === undefined || (teamPlace && project === undefined)) {
      return;
    }
    const refusedProject = teamPlace && project === null ? projectTexts.required : undefined;
    const refused = titleError(title);
    if (refusedProject !== undefined || refused) {
      setProjectRefusal(refusedProject);
      setTitleRefusal(refused ?? null);
      if (refusedProject !== undefined) {
        projectInput.current?.focus();
      }
      return;
    }
    const targetId = place.kind === 'project' ? place.projectId : project?.id;
    if (targetId === undefined) {
      return;
    }
    requestId.current ??= newRequestId();
    inFlight.current = true;
    setBusy(true);
    setAlert(null);
    try {
      onCreated(
        await submit(targetId, {
          requestId: requestId.current,
          typeId,
          title,
          team: { teamId: team?.id ?? null },
        }),
      );
    } catch (error) {
      const placed = placeRefusal(error, [
        'title',
        'team',
        ...(teamPlace ? (['project'] as const) : []),
      ]);
      setTitleRefusal(placed.fields.title ?? null);
      setTeamRefusal(placed.fields.team);
      setProjectRefusal(placed.fields.project);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.frame !== null);
      setBusy(false);
      if (placed.fields.team !== undefined) {
        void teams.refetch();
      }
      if (placed.fields.project !== undefined) {
        void projects.refetch();
      }
    } finally {
      inFlight.current = false;
    }
  }

  const chosenType = types.find((type) => type.id === typeId);
  const stateName = chosenType?.initialStateName ?? '';
  const teamsFailed = projectId !== undefined && teams.isError && !teams.isFetching;
  const projectsFailed = teamPlace !== null && projects.isError && !projects.isFetching;
  const projectsPending = teamPlace !== null && !projectsReady && !projectsFailed;
  const noProjectChosen = teamPlace !== null && project === null;
  // The project’s teams are being read after a pick: «Команда» is busy and «Создать» waits.
  const teamsPending = teamPlace !== null && project != null && !teamsReady && !teamsFailed;

  const caption =
    teamPlace !== null && project === null
      ? holdingCaption(teamPlace.team.name)
      : (teamDropped ?? (team !== undefined ? backlogCaption(team?.name ?? null) : null));
  const captionShown = chosenType?.onBacklog === true && caption !== null;
  // The options of «Команда»: the project’s teams; the view’s team alone while no project is
  // chosen or its teams are not read yet (it is the team shown, and is read-only or busy then). A
  // failed read has none, so the list says so (design § 6.4) and opening it reads them again.
  const teamChoices: readonly { id: string; name: string }[] | undefined =
    teamPlace === null
      ? teams.data
      : noProjectChosen
        ? [teamPlace.team]
        : teamsFailed
          ? undefined
          : (teams.data ?? [teamPlace.team]);

  return (
    <FormDialog
      open
      title="Новый рабочий элемент"
      submitLabel="Создать"
      busy={busy}
      error={alert}
      submitDisabled={team === undefined || projectsPending || projectsFailed || teamsPending}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <Typography variant="body2" color="text.secondary">
        {createdInStateLine(stateName)}
      </Typography>
      {teamPlace !== null &&
        (projectsFailed ? (
          <InlineNotice
            severity="error"
            action={{ label: 'Повторить', onClick: () => void projects.refetch() }}
          >
            {apiErrorText(projects.error)}
          </InlineNotice>
        ) : project === undefined ? (
          <FieldsSkeleton count={1} />
        ) : (
          <InlinePickerField<TeamProject>
            label={projectTexts.label}
            labelPlacement="top"
            frame="always"
            required
            placeholder={projectTexts.placeholder}
            options={projects.data ?? []}
            value={project}
            onChange={chooseProject}
            getOptionLabel={(option) => option.name}
            getOptionKey={(option) => option.id}
            isOptionEqualToValue={(option, chosen) => option.id === chosen.id}
            onOpen={() => {
              if (projects.isError) {
                void projects.refetch();
              }
            }}
            error={projectRefusal}
            busy={busy}
            ref={projectInput}
          />
        ))}
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
      {place.kind === 'project' && teamsFailed ? (
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
            teams={teamChoices}
            failed={teamPlace !== null && teamsFailed}
            onOpen={() => {
              if (teamPlace !== null && teams.isError) {
                void teams.refetch();
              }
            }}
            labelPlacement="top"
            frame="always"
            readOnly={noProjectChosen}
            onChange={(next) => {
              setTeam(next);
              setTeamRefusal(undefined);
              setTeamDropped(null);
            }}
            error={teamRefusal}
            busy={busy || teamsPending}
            inputRef={teamInput}
            describedBy={captionShown ? captionId : undefined}
          />
          {captionShown && (
            <Typography id={captionId} variant="caption" color="text.secondary">
              {caption}
            </Typography>
          )}
          <LiveRegion text={teamDropped ?? ''} serial={droppedSerial} />
        </>
      )}
      <TextField
        label="Заголовок"
        autoFocus={teamPlace === null}
        inputRef={titleInput}
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
