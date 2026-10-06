import { useId, useState, type FormEvent } from 'react';
import { useParams } from 'react-router-dom';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import FormHelperText from '@mui/material/FormHelperText';
import InputBase from '@mui/material/InputBase';
import Skeleton from '@mui/material/Skeleton';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useTheme, type Theme } from '@mui/material/styles';
import { useProcessDefinition } from '../../process';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { NotFound } from '../../../shared/ui/StateFrame';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type { WorkItem, WorkItemStateReference } from '../api/workItemsApi';
import { AreaPicker } from '../components/AreaPicker';
import { AssigneePicker } from '../components/AssigneePicker';
import { HeaderChrome } from '../components/HeaderChrome';
import { useNotice } from '../components/Notice';
import { RelatedWorkSection } from '../components/RelatedWorkSection';
import { StatePicker } from '../components/StatePicker';
import { useSaveWorkItem } from '../hooks/mutations';
import { useRefreshWorkItem, useWorkItem } from '../hooks/queries';
import { changesOf, draftOf, hasChanges, rebaseDraft, type WorkItemDraft } from '../lib/draft';
import { notFoundLevel, type WorkItemsNotFoundLevel } from '../lib/notFoundLevel';
import { allowedParentTypes, editableStates, parentHelperText, typeById } from '../lib/process';
import { isParentChanged, placeRefusal, type RefusalField } from '../lib/refusals';
import { descriptionError, titleError } from '../lib/rules';

/** Every field of the form a refusal can land on. */
const FORM_FIELDS: readonly RefusalField[] = [
  'title',
  'description',
  'state',
  'assignee',
  'area',
  'parent',
];

type FieldErrors = Partial<Record<RefusalField, string>>;

/** The band's stripe in the type's colour (mod-015 § 10.3: 4 px). */
function bandSx(appearance: string) {
  return (theme: Theme) => ({
    mt: 2,
    px: 2,
    py: 1.5,
    display: 'flex',
    alignItems: 'flex-start',
    gap: 2,
    backgroundColor: theme.palette.background.paper,
    borderLeft: `${theme.spacing(0.5)} solid ${theme.workTracking.typeAppearance(appearance).colour}`,
  });
}

/**
 * A work item's form (US-3, US-4, US-5; spec § Page: a work item's form;
 * mod-015 § 10.3 without its tabs, «Ещё действия», Итерация, Теги and
 * «Обсуждение»), a project-level page at `…/workitems/:identifier` (D-5). Its
 * read validates the whole address and answers the outermost level not found.
 * Keyed by the address, so another item starts with its own values. feat-001
 * adds the section «Связанная работа» below «Описание»: «Родитель» is a field
 * of the draft, and the children list is read-only.
 */
export function WorkItemFormPage() {
  const {
    organizationId = '',
    projectId = '',
    identifier = '',
  } = useParams<{ organizationId: string; projectId: string; identifier: string }>();
  return (
    <WorkItemForm
      key={`${organizationId}/${projectId}/${identifier}`}
      organizationId={organizationId}
      projectId={projectId}
      identifier={identifier}
    />
  );
}

function WorkItemForm({
  organizationId,
  projectId,
  identifier,
}: {
  organizationId: string;
  projectId: string;
  identifier: string;
}) {
  const query = useWorkItem(organizationId, projectId, identifier);
  const [gone, setGone] = useState<WorkItemsNotFoundLevel | null>(null);

  const level = gone ?? (query.isError ? notFoundLevel(query.error) : null);
  if (level) {
    // The item's own frame keeps «Рабочие элементы» marked: its project is
    // found, as mod-002 keeps «Команды» for a team that is not (its § 12).
    return <NotFound level={level} keepNavigation={level === 'workItem'} />;
  }
  if (query.isPending || (query.isError && query.isFetching)) {
    return <FormLoading />;
  }
  if (query.isError) {
    return (
      <>
        <PageHeader title="Рабочий элемент" />
        <ErrorBanner error={query.error} onRetry={() => void query.refetch()} />
      </>
    );
  }
  return (
    <LoadedForm
      organizationId={organizationId}
      projectId={projectId}
      identifier={identifier}
      item={query.data}
      onGone={setGone}
    />
  );
}

/**
 * Loading (spec § States): band and strip skeletons, `FormSkeleton` for
 * «Описание» and for «Связанная работа» (feat-001 spec § Screens and copy).
 * The second is hidden from assistive technology: one «Загрузка данных» is
 * announced for the page.
 */
function FormLoading() {
  return (
    <>
      <HeaderChrome />
      <Box aria-hidden="true" sx={{ mt: 2 }}>
        <Skeleton variant="rounded" sx={(theme) => ({ height: theme.spacing(10) })} />
        <Stack direction="row" spacing={2} sx={{ mt: 2 }}>
          {[0, 1, 2].map((index) => (
            <Skeleton
              key={index}
              variant="rounded"
              sx={(theme) => ({ width: theme.spacing(28), height: theme.spacing(5) })}
            />
          ))}
        </Stack>
      </Box>
      <Box sx={{ mt: 3 }}>
        <FormSkeleton fields={1} />
      </Box>
      <Box aria-hidden="true" sx={{ mt: 3 }}>
        <FormSkeleton fields={2} />
      </Box>
    </>
  );
}

function LoadedForm({
  organizationId,
  projectId,
  identifier,
  item,
  onGone,
}: {
  organizationId: string;
  projectId: string;
  identifier: string;
  item: WorkItem;
  onGone(level: WorkItemsNotFoundLevel): void;
}) {
  const theme = useTheme();
  const process = useProcessDefinition(organizationId);
  const save = useSaveWorkItem(organizationId, projectId, identifier);
  const refresh = useRefreshWorkItem(organizationId, projectId, identifier);
  const { notice, show } = useNotice();
  /** The user's edits; `null` until the first one, and again after a save. */
  const [edited, setEdited] = useState<WorkItemDraft | null>(null);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [alert, setAlert] = useState<string | null>(null);
  const titleHelperId = useId();

  const draft = edited ?? draftOf(item);
  const changes = changesOf(item, draft);
  const busy = save.isPending;

  function edit(field: RefusalField, patch: Partial<WorkItemDraft>) {
    setEdited((current) => ({ ...(current ?? draftOf(item)), ...patch }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  }

  async function handleSave(event: FormEvent) {
    event.preventDefault();
    if (busy || !hasChanges(changes)) {
      return;
    }
    const refused: FieldErrors = {};
    const title = changes.title !== undefined ? titleError(changes.title) : null;
    const description =
      changes.description !== undefined ? descriptionError(changes.description) : null;
    if (title) {
      refused.title = title;
    }
    if (description) {
      refused.description = description;
    }
    if (title || description) {
      setErrors((current) => ({ ...current, ...refused }));
      return;
    }
    setAlert(null);
    try {
      await save.mutateAsync(changes);
      setEdited(null);
      setErrors({});
      show('Изменения сохранены.');
    } catch (error) {
      const placed = placeRefusal(error, FORM_FIELDS);
      if (placed.frame) {
        onGone(placed.frame);
        return;
      }
      if (isParentChanged(error)) {
        // feat-001 BR-H-4: read the item again first, then «Родитель» shows
        // the stored parent; the user's other changes stay on the form.
        try {
          setEdited(rebaseDraft(item, await refresh(), draft));
        } catch (readError) {
          const gone = notFoundLevel(readError);
          if (gone) {
            onGone(gone);
            return;
          }
          setErrors(placed.fields);
          setAlert(apiErrorText(readError));
          return;
        }
      }
      setErrors(placed.fields);
      setAlert(placed.alert);
    }
  }

  // «Статус» offers the type's states without «Удалена» once the process is
  // read; until then, only the current one.
  const type = process.data ? typeById(process.data.workItemTypes, item.type.id) : undefined;
  const states: WorkItemStateReference[] = type ? editableStates(type) : [item.state];
  if (!states.some((state) => state.id === item.state.id)) {
    states.unshift(item.state);
  }
  // «Родитель»'s helper names the types the item's level allows once the
  // process is read (feat-001 api-contract § 1); until then, none.
  const parentHelper = process.data
    ? parentHelperText(allowedParentTypes(process.data, item.type.id))
    : null;
  const heading = `${item.type.name} ${item.identifier}`;

  return (
    <Box component="form" noValidate onSubmit={(event: FormEvent) => void handleSave(event)}>
      <HeaderChrome title={heading} />
      {alert && (
        <Alert severity="error" sx={{ mt: 2 }}>
          {alert}
        </Alert>
      )}
      <Box sx={bandSx(item.type.appearance)}>
        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography
            variant="subtitle2"
            component="h1"
            sx={{ display: 'flex', alignItems: 'center', gap: 1 }}
          >
            <TypeMark appearance={item.type.appearance} />
            {heading}
          </Typography>
          <InputBase
            value={draft.title}
            disabled={busy}
            fullWidth
            error={errors.title !== undefined}
            onChange={(event) => edit('title', { title: event.target.value })}
            inputProps={{
              'aria-label': 'Заголовок',
              'aria-required': true,
              'aria-invalid': errors.title !== undefined,
              'aria-describedby': errors.title !== undefined ? titleHelperId : undefined,
            }}
            sx={{ typography: 'h1', mt: 0.5 }}
          />
          {errors.title !== undefined && (
            <FormHelperText error id={titleHelperId}>
              {errors.title}
            </FormHelperText>
          )}
        </Box>
        <Button
          type="submit"
          variant="contained"
          disabled={busy || !hasChanges(changes)}
          startIcon={busy ? <CircularProgress size={theme.spacing(2)} /> : undefined}
          sx={{ flexShrink: 0 }}
        >
          Сохранить
        </Button>
      </Box>
      <Stack direction="row" spacing={2} useFlexGap sx={{ mt: 2, flexWrap: 'wrap' }}>
        <AssigneePicker
          organizationId={organizationId}
          value={draft.assignee}
          disabled={busy}
          error={errors.assignee}
          onChange={(assignee) => edit('assignee', { assignee })}
        />
        <StatePicker
          states={states}
          value={draft.stateId}
          disabled={busy}
          error={errors.state}
          onChange={(stateId) => edit('state', { stateId })}
        />
        <AreaPicker
          organizationId={organizationId}
          projectId={projectId}
          value={draft.area}
          disabled={busy}
          error={errors.area}
          onChange={(area) => edit('area', { area })}
        />
      </Stack>
      <Box component="section" sx={{ mt: 3 }}>
        <Typography
          variant="h2"
          component="h2"
          sx={{ pb: 1, mb: 2, borderBottom: 1, borderColor: 'divider' }}
        >
          Описание
        </Typography>
        <TextField
          label="Описание"
          placeholder="Добавьте описание"
          multiline
          minRows={6}
          fullWidth
          value={draft.description}
          disabled={busy}
          error={errors.description !== undefined}
          helperText={errors.description ?? 'До 100 000 символов'}
          onChange={(event) => edit('description', { description: event.target.value })}
        />
      </Box>
      <RelatedWorkSection
        organizationId={organizationId}
        projectId={projectId}
        identifier={identifier}
        stored={item.parent}
        childItems={item.children}
        value={draft.parent}
        disabled={busy}
        error={errors.parent}
        helperText={parentHelper}
        onChange={(parent) => edit('parent', { parent })}
      />
      {notice}
    </Box>
  );
}
