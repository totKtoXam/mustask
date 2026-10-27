import { useState } from 'react';
import Box from '@mui/material/Box';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import Typography from '@mui/material/Typography';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import {
  ConfirmDialog,
  FieldsSkeleton,
  FormDialog,
  InlineNotice,
  InlineTextArea,
  InlineTextField,
  Tag,
  TruncatedText,
} from '../../../shared/ui';
import { organizationErrorCodes } from '../../../shared/api/errorCodes';
import { ApiError } from '../../../shared/api/httpClient';
import type {
  Area,
  AreaNode,
  CreateAreaBody,
  ProjectListItem,
  TeamListItem,
} from '../api/organizationsApi';
import { useFocusInvalid } from '../hooks/useFocusInvalid';
import { useFocusOnMount } from '../hooks/useFocusOnMount';
import { useAreaDeletion, useAreaTree, useOrganizationTeams, useProjects } from '../hooks/queries';
import {
  TOP_LEVEL_OPTION,
  descendantCount,
  moveOptions,
  parentOptions,
  type PlaceOption,
} from '../lib/areaTree';
import { createAreaDialog, deleteAreaDialog, moveAreaDialog } from '../lib/areaTexts';
import { placeRefusal, type RefusalField } from '../lib/refusals';
import { descriptionError, nameError } from '../lib/validation';
import { LocalMultiPicker, LocalPicker } from './AreaPickers';

/** What every area dialog shares: the outcome hand-back (mod-002 design § 4.3). */
interface DialogEnd<R> {
  onSucceeded(result: R): void;
  /** `contextGone`: a not-found answer; the page reads itself again. */
  onClose(contextGone: boolean): void;
}

type FieldErrors = Partial<Record<RefusalField, string>>;

const placeLabel = (place: PlaceOption) => place.label;
const placeKey = (place: PlaceOption) => place.id ?? '__top__';

/**
 * «Новая область» (mod-018 design § 5.1; US-1): «Название» (1–100 once trimmed), «Родительская область»
 * («Верхний уровень» first, only with `area.create`; every area by its path), «Описание» (at most
 * 2 000, as typed), and «Проекты» / «Команды» as two searchable multiselects, each drawn only with
 * the link codes of both ends. Checked on «Создать»; nothing is sent while a field is refused.
 * Refusals land on their field with the input kept; the lists are read again after a refusal of
 * the parent or of a chosen end, and a choice no longer offered drops.
 */
export function CreateAreaDialog({
  organizationId,
  initialParentId,
  canTopLevel,
  canLinkProjects,
  canLinkTeams,
  submit,
  onSucceeded,
  onClose,
}: DialogEnd<Area> & {
  organizationId: string;
  /** `null`: the top level (from the header); an id: from a row menu or a page section. */
  initialParentId: string | null;
  /** The user holds `area.create`: «Верхний уровень» is offered. */
  canTopLevel: boolean;
  /** The user holds `area.link` and `project.link`. */
  canLinkProjects: boolean;
  /** The user holds `area.link` and `team.link`. */
  canLinkTeams: boolean;
  submit(body: CreateAreaBody): Promise<Area>;
}) {
  const tree = useAreaTree(organizationId, { fresh: true });
  const projectsQuery = useProjects(organizationId, 'active', {
    enabled: canLinkProjects,
    fresh: true,
  });
  const teamsQuery = useOrganizationTeams(organizationId, {
    enabled: canLinkTeams,
    fresh: true,
  });
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [parentId, setParentId] = useState<string | null>(initialParentId);
  const [projects, setProjects] = useState<ProjectListItem[]>([]);
  const [teams, setTeams] = useState<TeamListItem[]>([]);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusFirst = useFocusOnMount<HTMLInputElement>();
  const focusInvalid = useFocusInvalid();

  const areas = tree.data ?? [];
  const places = parentOptions(areas, canTopLevel);
  const parent =
    parentId === null
      ? canTopLevel
        ? TOP_LEVEL_OPTION
        : null
      : (places.find((place) => place.id === parentId) ?? null);

  async function handleSubmit() {
    const refused: FieldErrors = {};
    const nameRefusal = nameError(name);
    const descriptionRefusal = descriptionError(description);
    if (nameRefusal) refused.name = nameRefusal;
    if (descriptionRefusal) refused.description = descriptionRefusal;
    if (Object.keys(refused).length > 0) {
      setErrors(refused);
      focusInvalid();
      return;
    }
    const body: CreateAreaBody = { name };
    if (parent?.id) body.parentId = parent.id;
    if (description !== '') body.description = description;
    if (canLinkProjects && projects.length > 0) body.projectIds = projects.map((entry) => entry.id);
    if (canLinkTeams && teams.length > 0) body.teamIds = teams.map((entry) => entry.id);
    setBusy(true);
    setAlert(null);
    setErrors({});
    try {
      onSucceeded(await submit(body));
    } catch (error) {
      const fields: RefusalField[] = ['name', 'parent', 'description'];
      if (canLinkProjects) fields.push('projects');
      if (canLinkTeams) fields.push('teams');
      const placed = placeRefusal(error, fields);
      setErrors(placed.fields);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      focusInvalid();
      // The lists are read again; a choice no longer offered drops (design § 5.1).
      if (placed.fields.parent !== undefined) {
        void tree.refetch().then((result) => {
          if (parentId !== null && !result.data?.some((area) => area.id === parentId)) {
            setParentId(null);
          }
        });
      }
      if (placed.fields.projects !== undefined) {
        void projectsQuery.refetch().then((result) => {
          const offered = result.data;
          if (offered) {
            setProjects((current) => current.filter((p) => offered.some((o) => o.id === p.id)));
          }
        });
      }
      if (placed.fields.teams !== undefined) {
        void teamsQuery.refetch().then((result) => {
          const offered = result.data;
          if (offered) {
            setTeams((current) => current.filter((t) => offered.some((o) => o.id === t.id)));
          }
        });
      }
    }
  }

  return (
    <FormDialog
      open
      title={createAreaDialog.title}
      submitLabel={createAreaDialog.submit}
      busy={busy}
      error={alert}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <InlineTextField
        ref={focusFirst}
        label={createAreaDialog.name.label}
        frame="always"
        required
        hint={createAreaDialog.name.hint}
        value={name}
        busy={busy}
        error={errors.name}
        onChange={(next) => {
          setName(next);
          setErrors((current) => ({ ...current, name: undefined }));
        }}
      />
      <LocalPicker<PlaceOption>
        label={createAreaDialog.parent.label}
        hint={createAreaDialog.parent.hint}
        error={errors.parent}
        busy={busy}
        options={places}
        loading={tree.isPending}
        value={parent}
        onChange={(next) => {
          setParentId(next.id);
          setErrors((current) => ({ ...current, parent: undefined }));
        }}
        getKey={placeKey}
        getLabel={placeLabel}
      />
      <InlineTextArea
        label={createAreaDialog.description.label}
        frame="always"
        hint={createAreaDialog.description.hint}
        value={description}
        busy={busy}
        error={errors.description}
        onChange={(next) => {
          setDescription(next);
          setErrors((current) => ({ ...current, description: undefined }));
        }}
      />
      {canLinkProjects && (
        <LocalMultiPicker<ProjectListItem>
          label={createAreaDialog.projects.label}
          hint={createAreaDialog.projects.hint}
          error={errors.projects}
          busy={busy}
          options={projectsQuery.data ?? []}
          loading={projectsQuery.isPending}
          emptyText={projectsQuery.isError ? undefined : createAreaDialog.projects.none}
          value={projects}
          onChange={(next) => {
            setProjects(next);
            setErrors((current) => ({ ...current, projects: undefined }));
          }}
          getKey={(project) => project.id}
          getLabel={(project) => project.name}
          matchTexts={(project) => [project.key]}
          renderOption={(project) => (
            <Box sx={{ display: 'flex', gap: 1, minWidth: 0, alignItems: 'baseline' }}>
              <Box component="span">{project.name}</Box>
              <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                {project.key}
              </Typography>
            </Box>
          )}
        />
      )}
      {canLinkTeams && (
        <LocalMultiPicker<TeamListItem>
          label={createAreaDialog.teams.label}
          hint={createAreaDialog.teams.hint}
          error={errors.teams}
          busy={busy}
          options={teamsQuery.data ?? []}
          loading={teamsQuery.isPending}
          emptyText={teamsQuery.isError ? undefined : createAreaDialog.teams.none}
          value={teams}
          onChange={(next) => {
            setTeams(next);
            setErrors((current) => ({ ...current, teams: undefined }));
          }}
          getKey={(team) => team.id}
          getLabel={(team) => team.name}
        />
      )}
    </FormDialog>
  );
}

/**
 * «Перемещение области» (design § 5.3; US-2): names what moves, then «Новая родительская область»
 * with nothing preselected, offering «Верхний уровень» (unless the area is top-level, and only with
 * `area.create`) and every area except the area, its descendants and its current parent (D-5).
 * Refusals sit under the field with the tree unchanged; «Область не найдена» is the destination's
 * when the area is still in the tree, and the alert (the page read again) when it is not.
 */
export function MoveAreaDialog({
  organizationId,
  area,
  canTopLevel,
  submit,
  onSucceeded,
  onClose,
}: DialogEnd<{ parentId: string | null }> & {
  organizationId: string;
  area: AreaNode;
  canTopLevel: boolean;
  submit(parentId: string | null): Promise<unknown>;
}) {
  const tree = useAreaTree(organizationId, { fresh: true });
  const [parent, setParent] = useState<PlaceOption | null>(null);
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusField = useFocusOnMount<HTMLInputElement>();
  const focusInvalid = useFocusInvalid();
  const areas = tree.data ?? [];
  const options = moveOptions(areas, area.id, canTopLevel);
  const below = descendantCount(areas, area.id);

  async function handleSubmit() {
    if (!parent) {
      return;
    }
    setBusy(true);
    setAlert(null);
    setFieldError(undefined);
    try {
      await submit(parent.id);
      onSucceeded({ parentId: parent.id });
    } catch (error) {
      let placed = placeRefusal(error, ['parent']);
      if (error instanceof ApiError && error.errorCode === organizationErrorCodes.areaNotFound) {
        // The destination or the area itself: the tree says which.
        const result = await tree.refetch();
        if (!result.data?.some((candidate) => candidate.id === area.id)) {
          placed = { fields: {}, alert: deleteAreaDialog.notFound, contextGone: true };
        } else if (parent.id !== null && !result.data.some((c) => c.id === parent.id)) {
          setParent(null);
        }
      }
      setFieldError(placed.fields.parent);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      focusInvalid();
    }
  }

  return (
    <FormDialog
      open
      title={moveAreaDialog.title}
      submitLabel={moveAreaDialog.submit}
      busy={busy}
      error={alert}
      submitDisabled={parent === null}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <Typography variant="body2" color="text.secondary">
        {below > 0 ? moveAreaDialog.withChildren(area.name, below) : moveAreaDialog.leaf(area.name)}
      </Typography>
      <LocalPicker<PlaceOption>
        inputRef={focusField}
        label={moveAreaDialog.field}
        error={fieldError}
        busy={busy}
        options={options}
        loading={tree.isPending}
        value={parent}
        onChange={(next) => {
          setParent(next);
          setFieldError(undefined);
        }}
        getKey={placeKey}
        getLabel={placeLabel}
      />
    </FormDialog>
  );
}

/**
 * «Удаление области» (design § 5.4; BR-AW-9): destructive, focus on «Отмена». Every link is named —
 * the projects (a deactivated one tagged) and the teams — read when the dialog opens. An area the tree
 * knows to have children says so in place of the question and its links are not read. A refusal is
 * the alert, and the page reads itself again when the dialog closes.
 */
export function DeleteAreaDialog({
  organizationId,
  area,
  hasChildren,
  action,
  onSucceeded,
  onClose,
}: DialogEnd<unknown> & {
  organizationId: string;
  area: { id: string; name: string };
  /** The tree knows a child of the area. */
  hasChildren: boolean;
  action(): Promise<unknown>;
}) {
  const links = useAreaDeletion(organizationId, area.id, { enabled: !hasChildren });
  const [busy, setBusy] = useState(false);
  const [alert, setAlert] = useState<string | null>(null);
  const [stale, setStale] = useState(false);

  async function confirm() {
    setBusy(true);
    setAlert(null);
    try {
      onSucceeded(await action());
    } catch (error) {
      setAlert(placeRefusal(error, []).alert);
      // The area, its children or the code changed: whatever the page shows is no longer true.
      setStale(true);
      setBusy(false);
    }
  }

  const counting = !hasChildren && (links.isPending || (links.isError && links.isFetching));
  const projects = links.data?.projects ?? [];
  const teams = links.data?.teams ?? [];
  const hasLinks = projects.length + teams.length > 0;

  return (
    <ConfirmDialog
      open
      title={deleteAreaDialog.title}
      confirmLabel={deleteAreaDialog.confirm}
      destructive
      cancelAutoFocus
      busy={busy}
      error={alert}
      confirmDisabled={hasChildren || !links.isSuccess}
      onConfirm={() => void confirm()}
      onClose={() => onClose(stale)}
    >
      {hasChildren ? (
        <InlineNotice severity="warning">{deleteAreaDialog.hasChildren}</InlineNotice>
      ) : counting ? (
        <FieldsSkeleton count={2} />
      ) : links.isError ? (
        <InlineNotice
          severity="error"
          action={{ label: 'Повторить', onClick: () => void links.refetch() }}
        >
          {apiErrorText(links.error)}
        </InlineNotice>
      ) : hasLinks ? (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          <Typography variant="body1">{deleteAreaDialog.withLinks(area.name)}</Typography>
          <Box
            sx={(theme) => ({ maxHeight: theme.spacing(40), overflowY: 'auto' })}
            tabIndex={0}
            role="group"
            aria-label={deleteAreaDialog.title}
          >
            {teams.length > 0 && (
              <>
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                  {deleteAreaDialog.teamsList(teams.length)}
                </Typography>
                <List dense disablePadding>
                  {teams.map((team) => (
                    <ListItem key={team.id} disableGutters>
                      <TruncatedText>{team.name}</TruncatedText>
                    </ListItem>
                  ))}
                </List>
              </>
            )}
            {projects.length > 0 && (
              <>
                <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                  {deleteAreaDialog.projectsList(projects.length)}
                </Typography>
                <List dense disablePadding>
                  {projects.map((project) => (
                    <ListItem
                      key={project.id}
                      disableGutters
                      sx={{ display: 'flex', alignItems: 'center', gap: 1 }}
                    >
                      <TruncatedText>{project.name}</TruncatedText>
                      {!project.active && <Tag label={deleteAreaDialog.deactivated} />}
                    </ListItem>
                  ))}
                </List>
              </>
            )}
          </Box>
          <Typography variant="body1">{deleteAreaDialog.irreversible}</Typography>
        </Box>
      ) : (
        <Typography variant="body1">{deleteAreaDialog.noLinks(area.name)}</Typography>
      )}
    </ConfirmDialog>
  );
}
