import { useState } from 'react';
import Typography from '@mui/material/Typography';
import { organizationErrorCodes } from '../../../shared/api/errorCodes';
import { ApiError } from '../../../shared/api/httpClient';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { FormDialog } from '../../../shared/ui';
import type { ProjectTeam } from '../api/organizationsApi';
import { useAreaTree, useTeams } from '../hooks/queries';
import { useFocusInvalid } from '../hooks/useFocusInvalid';
import { useFocusOnMount } from '../hooks/useFocusOnMount';
import { deleteAreaDialog, placeProjectDialog } from '../lib/areaTexts';
import { placeRefusal } from '../lib/refusals';
import { LocalPicker } from './AreaPickers';
import { ProjectTeamsRefusal, UnlinkProjectTeamDialog } from './ProjectTeamsRefusal';

/** One project the dialog offers: its node, with where it stands now. */
interface ProjectPlace {
  /** The project's root node (what the move addresses). */
  nodeId: string;
  /** The project itself: its teams are listed when the move is refused. */
  projectId: string | null;
  name: string;
  parentId: string | null;
  parentName: string | null;
}

const placeKey = (project: ProjectPlace) => project.nodeId;
const placeLabel = (project: ProjectPlace) =>
  placeProjectDialog.current(project.name, project.parentName ?? '—');

/**
 * «Добавление проекта в департамент» (feat-043 design § 8.2): one project at a time, from the active
 * projects that stand in another department (read from the area tree at open), each with its present
 * place. Once one is chosen the dialog says what moves from where. A project with a linked team is
 * refused by the server (BR-TD-4, `project_has_teams`) and the server's text is the dialog's alert. A refusal of the move lands under the field,
 * a project that is gone drops from the choice and the offer is read again; the department gone is
 * the dialog's alert and the page reads itself again.
 */
export function PlaceProjectDialog({
  organizationId,
  department,
  writableIds,
  submit,
  onSucceeded,
  onClose,
}: {
  organizationId: string;
  department: { id: string; name: string };
  /** The nodes the caller may write (mod-019 § 5, BR-MX-3): a project is offered only with the code on its node and its current parent. Absent: all. */
  writableIds?: ReadonlySet<string>;
  submit(nodeId: string): Promise<unknown>;
  onSucceeded(project: { name: string }): void;
  /** `contextGone`: the department answered not found; the page reads itself again. */
  onClose(contextGone: boolean): void;
}) {
  const tree = useAreaTree(organizationId, { fresh: true });
  const [chosen, setChosen] = useState<ProjectPlace | null>(null);
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  /** The server's text of `project_has_teams` while the refusal and its teams are up (BR-TD-4). */
  const [linksText, setLinksText] = useState<string | null>(null);
  const [unlinking, setUnlinking] = useState<ProjectTeam | null>(null);
  const linkedTeams = useTeams(organizationId, chosen?.projectId ?? '', {
    enabled: linksText !== null && Boolean(chosen?.projectId),
    fresh: true,
  });
  const focusField = useFocusOnMount<HTMLInputElement>();
  const focusInvalid = useFocusInvalid();

  const nodes = tree.data ?? [];
  const options: ProjectPlace[] = nodes
    .filter(
      (node) =>
        node.type === 'project' &&
        node.projectActive !== false &&
        node.parentId !== department.id &&
        node.parentId !== null &&
        (writableIds === undefined ||
          (writableIds.has(node.id) && (node.parentId === null || writableIds.has(node.parentId)))),
    )
    .map((node) => ({
      nodeId: node.id,
      projectId: node.projectId,
      name: node.name,
      parentId: node.parentId,
      parentName: nodes.find((candidate) => candidate.id === node.parentId)?.name ?? null,
    }));

  async function handleSubmit() {
    if (!chosen) {
      return;
    }
    setBusy(true);
    setAlert(null);
    setFieldError(undefined);
    setLinksText(null);
    try {
      await submit(chosen.nodeId);
      onSucceeded({ name: chosen.name });
    } catch (error) {
      if (
        error instanceof ApiError &&
        error.errorCode === organizationErrorCodes.areaProjectHasTeams
      ) {
        setLinksText(apiErrorText(error));
        setBusy(false);
        return;
      }
      let placed = placeRefusal(error, ['location']);
      if (error instanceof ApiError && error.errorCode === organizationErrorCodes.areaNotFound) {
        // The project or the department: the tree says which.
        const result = await tree.refetch();
        if (!result.data?.some((candidate) => candidate.id === department.id)) {
          placed = { fields: {}, alert: deleteAreaDialog.notFound, contextGone: true };
        } else {
          placed = {
            fields: { location: placeProjectDialog.projectNotFound },
            alert: null,
            contextGone: false,
          };
          setChosen(null);
        }
      }
      setFieldError(placed.fields.location);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      focusInvalid();
    }
  }

  const teamRows = linkedTeams.data ?? [];
  const refusal =
    linksText !== null && !(linkedTeams.isSuccess && teamRows.length === 0) ? (
      <ProjectTeamsRefusal text={linksText} teams={teamRows} onUnlink={setUnlinking} />
    ) : null;

  if (unlinking && chosen?.projectId) {
    return (
      <UnlinkProjectTeamDialog
        organizationId={organizationId}
        projectId={chosen.projectId}
        projectName={chosen.name}
        team={unlinking}
        onDone={() => setUnlinking(null)}
        onClose={(gone) => {
          setUnlinking(null);
          if (gone) {
            setContextGone(true);
          }
        }}
      />
    );
  }

  return (
    <FormDialog
      open
      title={placeProjectDialog.title}
      submitLabel={placeProjectDialog.submit}
      busy={busy}
      error={alert ?? refusal}
      submitDisabled={chosen === null}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <LocalPicker<ProjectPlace>
        inputRef={focusField}
        label={placeProjectDialog.field}
        required
        error={fieldError}
        busy={busy}
        options={options}
        loading={tree.isPending}
        value={chosen}
        onChange={(next) => {
          setChosen(next);
          setFieldError(undefined);
        }}
        getKey={placeKey}
        getLabel={placeLabel}
      />
      {chosen && (
        <Typography variant="body2" color="text.secondary">
          {placeProjectDialog.fromPlace(chosen.name, chosen.parentName ?? '—', department.name)}
        </Typography>
      )}
    </FormDialog>
  );
}
