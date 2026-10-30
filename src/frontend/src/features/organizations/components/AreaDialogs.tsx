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
  TruncatedText,
} from '../../../shared/ui';
import { organizationErrorCodes } from '../../../shared/api/errorCodes';
import { ApiError } from '../../../shared/api/httpClient';
import type { Area, AreaNode, CreateAreaBody, TeamListItem } from '../api/organizationsApi';
import { useFocusInvalid } from '../hooks/useFocusInvalid';
import { useFocusOnMount } from '../hooks/useFocusOnMount';
import {
  useAreaDeletion,
  useAreaTree,
  useOrganizationTeams,
  useProjectAreaTree,
} from '../hooks/queries';
import {
  areaMoveOptions,
  descendantCount,
  projectMoveOptions,
  subAreaParentOptions,
  type PlaceOption,
} from '../lib/areaTree';
import {
  createDepartmentDialog,
  createSubAreaDialog,
  deleteAreaDialog,
  moveAreaDialog,
} from '../lib/areaTexts';
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
 * «Новый департамент» (mod-018 design § 5.1; US-1): «Название» (1–100 once trimmed), «Описание» (at
 * most 2 000, as typed) and «Команды» as a searchable multiselect, drawn only with the link codes of
 * both ends. There is no parent field: a department stands at the top (OQ-3 A). Checked on «Создать»;
 * nothing is sent while a field is refused. Refusals land on their field with the input kept; the team
 * list is read again after a refusal of a chosen team, and a choice no longer offered drops.
 */
export function CreateDepartmentDialog({
  organizationId,
  canLinkTeams,
  submit,
  onSucceeded,
  onClose,
}: DialogEnd<Area> & {
  organizationId: string;
  /** The user holds `area.link` and `team.link`. */
  canLinkTeams: boolean;
  submit(body: CreateAreaBody): Promise<Area>;
}) {
  const teamsQuery = useOrganizationTeams(organizationId, {
    enabled: canLinkTeams,
    fresh: true,
  });
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [teams, setTeams] = useState<TeamListItem[]>([]);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusFirst = useFocusOnMount<HTMLInputElement>();
  const focusInvalid = useFocusInvalid();

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
    if (description !== '') body.description = description;
    if (canLinkTeams && teams.length > 0) body.teamIds = teams.map((entry) => entry.id);
    setBusy(true);
    setAlert(null);
    setErrors({});
    try {
      onSucceeded(await submit(body));
    } catch (error) {
      const fields: RefusalField[] = ['name', 'description'];
      if (canLinkTeams) fields.push('teams');
      const placed = placeRefusal(error, fields);
      setErrors(placed.fields);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      focusInvalid();
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
      title={createDepartmentDialog.title}
      submitLabel={createDepartmentDialog.submit}
      busy={busy}
      error={alert}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <InlineTextField
        ref={focusFirst}
        label={createDepartmentDialog.name.label}
        frame="always"
        required
        hint={createDepartmentDialog.name.hint}
        value={name}
        busy={busy}
        error={errors.name}
        onChange={(next) => {
          setName(next);
          setErrors((current) => ({ ...current, name: undefined }));
        }}
      />
      <InlineTextArea
        label={createDepartmentDialog.description.label}
        frame="always"
        hint={createDepartmentDialog.description.hint}
        value={description}
        busy={busy}
        error={errors.description}
        onChange={(next) => {
          setDescription(next);
          setErrors((current) => ({ ...current, description: undefined }));
        }}
      />
      {canLinkTeams && (
        <LocalMultiPicker<TeamListItem>
          label={createDepartmentDialog.teams.label}
          hint={createDepartmentDialog.teams.hint}
          error={errors.teams}
          busy={busy}
          options={teamsQuery.data ?? []}
          loading={teamsQuery.isPending}
          emptyText={teamsQuery.isError ? undefined : createDepartmentDialog.teams.none}
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
 * «Новая вложенная область» (design § 5.2; US-1): «Название», «Родительская область» (not clearable)
 * over **the project's own subtree only** — its node and its sub-areas by path — and «Описание».
 * The parent is preselected from a row menu or a page and changeable within that project. A refusal
 * of the parent re-reads the subtree; a choice no longer offered drops.
 */
export function CreateSubAreaDialog({
  organizationId,
  projectId,
  initialParentId,
  submit,
  onSucceeded,
  onClose,
}: DialogEnd<Area> & {
  organizationId: string;
  projectId: string;
  initialParentId: string | null;
  submit(body: CreateAreaBody): Promise<Area>;
}) {
  const tree = useProjectAreaTree(organizationId, projectId, { fresh: true });
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [parentId, setParentId] = useState<string | null>(initialParentId);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusFirst = useFocusOnMount<HTMLInputElement>();
  const focusInvalid = useFocusInvalid();

  const nodes = tree.data ?? [];
  const places = subAreaParentOptions(nodes);
  const parent = places.find((place) => place.id === (parentId ?? nodes[0]?.id)) ?? null;
  const projectName = nodes.find((node) => node.type === 'project')?.name ?? '';

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
    if (!parent?.id) {
      return;
    }
    const body: CreateAreaBody = { name, parentId: parent.id };
    if (description !== '') body.description = description;
    setBusy(true);
    setAlert(null);
    setErrors({});
    try {
      onSucceeded(await submit(body));
    } catch (error) {
      const placed = placeRefusal(error, ['name', 'parent', 'description']);
      setErrors(placed.fields);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      focusInvalid();
      if (placed.fields.parent !== undefined) {
        void tree.refetch().then((result) => {
          if (parentId !== null && !result.data?.some((node) => node.id === parentId)) {
            setParentId(null);
          }
        });
      }
    }
  }

  return (
    <FormDialog
      open
      title={createSubAreaDialog.title}
      submitLabel={createSubAreaDialog.submit}
      busy={busy}
      error={alert}
      submitDisabled={parent === null}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <InlineTextField
        ref={focusFirst}
        label={createSubAreaDialog.name.label}
        frame="always"
        required
        hint={createSubAreaDialog.name.hint}
        value={name}
        busy={busy}
        error={errors.name}
        onChange={(next) => {
          setName(next);
          setErrors((current) => ({ ...current, name: undefined }));
        }}
      />
      <LocalPicker<PlaceOption>
        label={createSubAreaDialog.parent.label}
        required
        hint={projectName ? createSubAreaDialog.parent.helper(projectName) : undefined}
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
        label={createSubAreaDialog.description.label}
        frame="always"
        hint={createSubAreaDialog.description.hint}
        value={description}
        busy={busy}
        error={errors.description}
        onChange={(next) => {
          setDescription(next);
          setErrors((current) => ({ ...current, description: undefined }));
        }}
      />
    </FormDialog>
  );
}

/** The node a move dialog works on. */
export type MovableNode = Pick<AreaNode, 'id' | 'name' | 'type' | 'parentId' | 'projectId'>;

/**
 * The move, per type (design § 5.4; BR-AW-3): a project's «Перемещение проекта» offers «Организация»
 * and the departments; a sub-area's «Перемещение области» offers its own project's nodes. Nothing is
 * preselected; «Переместить» waits for a place. Refusals sit under the field with the tree unchanged;
 * «Область не найдена» is the destination's when the node is still in the tree and the alert (the
 * page read again) when it is not. Moving to or from the top level needs `area.create`.
 */
export function MoveAreaDialog({
  organizationId,
  node,
  canTopLevel,
  submit,
  onSucceeded,
  onClose,
}: DialogEnd<{ parentId: string | null }> & {
  organizationId: string;
  node: MovableNode;
  canTopLevel: boolean;
  submit(parentId: string | null): Promise<unknown>;
}) {
  const isProject = node.type === 'project';
  const orgTree = useAreaTree(organizationId, { enabled: isProject, fresh: true });
  const projectTree = useProjectAreaTree(organizationId, node.projectId ?? '', {
    enabled: !isProject && node.projectId !== null,
    fresh: true,
  });
  const tree = isProject ? orgTree : projectTree;
  const texts = isProject ? moveAreaDialog.project : moveAreaDialog.area;
  const [parent, setParent] = useState<PlaceOption | null>(null);
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const [alert, setAlert] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [contextGone, setContextGone] = useState(false);
  const focusField = useFocusOnMount<HTMLInputElement>();
  const focusInvalid = useFocusInvalid();
  const nodes = tree.data ?? [];
  const options = (
    isProject
      ? projectMoveOptions(nodes, node.parentId)
      : areaMoveOptions(nodes, node.id, node.parentId)
  ).filter((place) => place.id !== null || canTopLevel);
  const below = descendantCount(nodes, node.id);
  const field: RefusalField = isProject ? 'location' : 'parent';

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
      let placed = placeRefusal(error, [field]);
      if (error instanceof ApiError && error.errorCode === organizationErrorCodes.areaNotFound) {
        // The destination or the node itself: the tree says which.
        const result = await tree.refetch();
        if (!result.data?.some((candidate) => candidate.id === node.id)) {
          placed = { fields: {}, alert: deleteAreaDialog.notFound, contextGone: true };
        } else if (parent.id !== null && !result.data.some((c) => c.id === parent.id)) {
          setParent(null);
        }
      }
      setFieldError(placed.fields[field]);
      setAlert(placed.alert);
      setContextGone((gone) => gone || placed.contextGone);
      setBusy(false);
      focusInvalid();
    }
  }

  return (
    <FormDialog
      open
      title={texts.title}
      submitLabel={moveAreaDialog.submit}
      busy={busy}
      error={alert}
      submitDisabled={parent === null}
      onSubmit={() => void handleSubmit()}
      onClose={() => onClose(contextGone)}
    >
      <Typography variant="body2" color="text.secondary">
        {below > 0 ? texts.withChildren(node.name, below) : texts.leaf(node.name)}
      </Typography>
      <LocalPicker<PlaceOption>
        inputRef={focusField}
        label={texts.field}
        required
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
 * The deletion (design § 5.5; BR-AW-9): destructive, focus on «Отмена». A department names its team
 * links, read when the dialog opens; a sub-area has none to count. A node the tree knows to have
 * children says so in place of the question. A refusal is the alert, and the page reads itself
 * again when the dialog closes. A project node is never deleted here: it goes with its project.
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
  area: { id: string; name: string; type: 'department' | 'area' };
  /** The tree knows a child of the node. */
  hasChildren: boolean;
  action(): Promise<unknown>;
}) {
  const isDepartment = area.type === 'department';
  const links = useAreaDeletion(organizationId, area.id, { enabled: isDepartment && !hasChildren });
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
      // The node, its children or the code changed: whatever the page shows is no longer true.
      setStale(true);
      setBusy(false);
    }
  }

  const counting =
    isDepartment && !hasChildren && (links.isPending || (links.isError && links.isFetching));
  const teams = links.data?.teams ?? [];
  const ready = hasChildren ? false : isDepartment ? links.isSuccess : true;

  return (
    <ConfirmDialog
      open
      title={isDepartment ? deleteAreaDialog.departmentTitle : deleteAreaDialog.areaTitle}
      confirmLabel={
        isDepartment ? deleteAreaDialog.departmentConfirm : deleteAreaDialog.areaConfirm
      }
      destructive
      cancelAutoFocus
      busy={busy}
      error={alert}
      confirmDisabled={!ready}
      onConfirm={() => void confirm()}
      onClose={() => onClose(stale)}
    >
      {hasChildren ? (
        <InlineNotice severity="warning">{deleteAreaDialog.hasChildren}</InlineNotice>
      ) : counting ? (
        <FieldsSkeleton count={1} />
      ) : isDepartment && links.isError ? (
        <InlineNotice
          severity="error"
          action={{ label: 'Повторить', onClick: () => void links.refetch() }}
        >
          {apiErrorText(links.error)}
        </InlineNotice>
      ) : !isDepartment ? (
        <Typography variant="body1">{deleteAreaDialog.area(area.name)}</Typography>
      ) : teams.length > 0 ? (
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          <Typography variant="body1">{deleteAreaDialog.withTeams(area.name)}</Typography>
          <Box
            sx={(theme) => ({ maxHeight: theme.spacing(40), overflowY: 'auto' })}
            tabIndex={0}
            role="group"
            aria-label={deleteAreaDialog.departmentTitle}
          >
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
          </Box>
          <Typography variant="body1">{deleteAreaDialog.departmentIrreversible}</Typography>
        </Box>
      ) : (
        <Typography variant="body1">{deleteAreaDialog.departmentNoTeams(area.name)}</Typography>
      )}
    </ConfirmDialog>
  );
}
