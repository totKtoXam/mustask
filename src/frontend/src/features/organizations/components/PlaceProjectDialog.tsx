import { useState } from 'react';
import Typography from '@mui/material/Typography';
import { organizationErrorCodes } from '../../../shared/api/errorCodes';
import { ApiError } from '../../../shared/api/httpClient';
import { FormDialog } from '../../../shared/ui';
import { useAreaTree } from '../hooks/queries';
import { useFocusInvalid } from '../hooks/useFocusInvalid';
import { useFocusOnMount } from '../hooks/useFocusOnMount';
import { deleteAreaDialog, placeProjectDialog } from '../lib/areaTexts';
import { placeRefusal } from '../lib/refusals';
import { LocalPicker } from './AreaPickers';

/** One project the dialog offers: its node, with where it stands now. */
interface ProjectPlace {
  /** The project's root node (what the move addresses). */
  nodeId: string;
  name: string;
  /** `null`: the top level. */
  parentId: string | null;
  parentName: string | null;
}

const placeKey = (project: ProjectPlace) => project.nodeId;
const placeLabel = (project: ProjectPlace) =>
  placeProjectDialog.current(project.name, project.parentName ?? placeProjectDialog.topLevelNow);

/**
 * «Добавление проекта в департамент» (feat-043 design § 8.2): one project at a time, from the active
 * projects that stand elsewhere (read from the area tree at open), each with its present place. Once
 * one is chosen the dialog says what moves from where. A refusal of the move lands under the field,
 * a project that is gone drops from the choice and the offer is read again; the department gone is
 * the dialog's alert and the page reads itself again.
 */
export function PlaceProjectDialog({
  organizationId,
  department,
  canFromTop,
  submit,
  onSucceeded,
  onClose,
}: {
  organizationId: string;
  department: { id: string; name: string };
  /** The user holds `area.create`: a move from the top level needs it. */
  canFromTop: boolean;
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
  const focusField = useFocusOnMount<HTMLInputElement>();
  const focusInvalid = useFocusInvalid();

  const nodes = tree.data ?? [];
  const options: ProjectPlace[] = nodes
    .filter(
      (node) =>
        node.type === 'project' &&
        node.projectActive !== false &&
        node.parentId !== department.id &&
        (canFromTop || node.parentId !== null),
    )
    .map((node) => ({
      nodeId: node.id,
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
    try {
      await submit(chosen.nodeId);
      onSucceeded({ name: chosen.name });
    } catch (error) {
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

  return (
    <FormDialog
      open
      title={placeProjectDialog.title}
      submitLabel={placeProjectDialog.submit}
      busy={busy}
      error={alert}
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
          {chosen.parentName === null
            ? placeProjectDialog.fromTop(chosen.name, department.name)
            : placeProjectDialog.fromPlace(chosen.name, chosen.parentName, department.name)}
        </Typography>
      )}
    </FormDialog>
  );
}
