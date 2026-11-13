import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { organizationErrorCodes } from '../../../shared/api/errorCodes';
import { ApiError } from '../../../shared/api/httpClient';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { ActionButton, FormDialog, TruncatedText } from '../../../shared/ui';
import type { AreaNode, TeamPlace, TeamProject } from '../api/organizationsApi';
import { useAreaTree } from '../hooks/queries';
import { useFocusInvalid } from '../hooks/useFocusInvalid';
import { useFocusOnMount } from '../hooks/useFocusOnMount';
import { departmentsOf } from '../lib/areaTree';
import { placeRefusal } from '../lib/refusals';
import { moveTeamDialog } from '../lib/teamTexts';
import { LocalPicker } from './AreaPickers';

/**
 * «Перемещение команды» (doc-016 design § 4.2, BR-TD-4 team part): one required «Новый департамент»
 * over the organization's other departments, read when the dialog opens. A team linked to a project
 * is refused (409 `organizations.team.has_links`): the dialog stays open with the server's text and,
 * below it, the team's projects, each with «Отключить» — the host opens the unlink dialog and mounts
 * this one again with the chosen department kept (`initialDepartmentId`, `initiallyRefused`). A
 * department that is gone or no longer fits lands under the field. Mounted per opening.
 */
export function MoveTeamDialog({
  organizationId,
  team,
  projects,
  initialDepartmentId,
  initialRefusal,
  submit,
  onUnlink,
  onSucceeded,
  onClose,
}: {
  organizationId: string;
  team: { id: string; name: string; departmentId: string };
  /** The team's active projects: what a refusal names. */
  projects: readonly TeamProject[];
  initialDepartmentId?: string | null;
  /** The dialog returns after an unlink: the refusal's text and list stay up until the next try. */
  initialRefusal?: string | null;
  submit(departmentId: string): Promise<TeamPlace>;
  /** «Отключить» on a project of the refusal: the host takes over. */
  onUnlink(project: TeamProject, departmentId: string | null, refusal: string): void;
  onSucceeded(place: TeamPlace): void;
  /** `contextGone`: the team answered not found; the page reads itself again. */
  onClose(contextGone: boolean): void;
}) {
  const tree = useAreaTree(organizationId, { fresh: true });
  const [departmentId, setDepartmentId] = useState<string | null>(initialDepartmentId ?? null);
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const [alert, setAlert] = useState<string | null>(null);
  /** The server's text of `organizations.team.has_links`: set while the refusal and its list are up. */
  const [linksText, setLinksText] = useState<string | null>(initialRefusal ?? null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusField = useFocusOnMount<HTMLInputElement>();
  const focusInvalid = useFocusInvalid();

  const options = departmentsOf(tree.data ?? []).filter((node) => node.id !== team.departmentId);
  const chosen = options.find((node) => node.id === departmentId) ?? null;

  async function handleSubmit() {
    if (departmentId === null) {
      setFieldError(moveTeamDialog.required);
      focusInvalid();
      return;
    }
    setBusy(true);
    setAlert(null);
    setFieldError(undefined);
    setLinksText(null);
    try {
      onSucceeded(await submit(departmentId));
    } catch (error) {
      if (error instanceof ApiError && error.errorCode === organizationErrorCodes.teamHasLinks) {
        setLinksText(apiErrorText(error));
      } else {
        const placed = placeRefusal(error, ['department']);
        setFieldError(placed.fields.department);
        setAlert(placed.alert);
        setContextGone((gone) => gone || placed.contextGone);
        if (placed.fields.department !== undefined) {
          void tree.refetch().then((result) => {
            if (
              result.data &&
              !departmentsOf(result.data).some((node) => node.id === departmentId)
            ) {
              setDepartmentId(null);
            }
          });
        }
      }
      setBusy(false);
      focusInvalid();
    }
  }

  const refusalBlock =
    linksText !== null ? (
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
        <Typography variant="body2">{linksText}</Typography>
        <Typography variant="body2">{moveTeamDialog.projects}</Typography>
        {projects.map((project) => (
          <Box key={project.id} sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <TruncatedText>{project.name}</TruncatedText>
            <ActionButton
              label={moveTeamDialog.unlink}
              tone="subtle"
              size="compact"
              aria-label={moveTeamDialog.unlinkRow(project.name)}
              onClick={() => onUnlink(project, departmentId, linksText)}
            />
          </Box>
        ))}
      </Box>
    ) : null;

  return (
    <FormDialog
      open
      title={moveTeamDialog.title(team.name)}
      submitLabel={moveTeamDialog.submit}
      busy={busy}
      error={alert ?? refusalBlock}
      submitDisabled={options.length === 0 && tree.isSuccess}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      {tree.isSuccess && options.length === 0 ? (
        <Typography variant="body1">{moveTeamDialog.noOther}</Typography>
      ) : (
        <LocalPicker<AreaNode>
          inputRef={focusField}
          label={moveTeamDialog.field}
          required
          hint={moveTeamDialog.hint}
          error={fieldError}
          busy={busy}
          options={options}
          loading={tree.isPending}
          value={chosen}
          onChange={(next) => {
            setDepartmentId(next.id);
            setFieldError(undefined);
          }}
          getKey={(node) => node.id}
          getLabel={(node) => node.name}
        />
      )}
    </FormDialog>
  );
}
