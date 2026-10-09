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
import useMediaQuery from '@mui/material/useMediaQuery';
import { useTheme, type Theme } from '@mui/material/styles';
import { useProcessDefinition } from '../../process';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { ErrorBanner } from '../../../shared/ui/ErrorBanner';
import { FormSkeleton } from '../../../shared/ui/FormSkeleton';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { NotFound } from '../../../shared/ui/StateFrame';
import { TypeMark } from '../../../shared/ui/WorkTrackingMarks';
import type { WorkItem, WorkItemFieldName, WorkItemStateReference } from '../api/workItemsApi';
import { AreaPicker } from '../components/AreaPicker';
import { AssigneePicker } from '../components/AssigneePicker';
import { FieldControl } from '../components/FieldControl';
import { FormSection } from '../components/FormSection';
import { HeaderChrome } from '../components/HeaderChrome';
import { useNotice } from '../components/Notice';
import { RelatedWorkSection } from '../components/RelatedWorkSection';
import { StatePicker } from '../components/StatePicker';
import { useSaveWorkItem } from '../hooks/mutations';
import { useRefreshWorkItem, useWorkItem } from '../hooks/queries';
import {
  changesOf,
  draftErrors,
  draftOf,
  isDirty,
  rebaseDraft,
  type WorkItemDraft,
} from '../lib/draft';
import { TEXT_FIELDS, TEXT_HELPER, fieldsOf, layoutOf } from '../lib/fields';
import { notFoundLevel, type WorkItemsNotFoundLevel } from '../lib/notFoundLevel';
import { allowedParentTypes, editableStates, parentHelperText, typeById } from '../lib/process';
import { isParentChanged, placeRefusal, type RefusalField } from '../lib/refusals';

/** mod-003's fields of the form a refusal can land on; feat-022 adds the type's own. */
const FORM_FIELDS: readonly RefusalField[] = [
  'title',
  'description',
  'state',
  'assignee',
  'area',
  'parent',
];

type FieldErrors = Partial<Record<RefusalField, string>>;

/**
 * The «Детали» grid (mod-015 § 10.3; feat-022 spec § The form's sections): two
 * columns, 8/4, at ≥ 1280 px; one below, the left column's sections first.
 */
function gridSx(wide: boolean) {
  return {
    mt: 3,
    display: 'grid',
    gridTemplateColumns: wide ? 'minmax(0, 2fr) minmax(0, 1fr)' : 'minmax(0, 1fr)',
    gap: 3,
    alignItems: 'start',
  } as const;
}

/** Whether the form stands in two columns: `lg` is 1280 px (ADR-0024). */
function useWideForm(): boolean {
  const theme = useTheme();
  return useMediaQuery(theme.breakpoints.up('lg'));
}

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
 * adds the section «Связанная работа»: «Родитель» is a field of the draft, and
 * the children list is read-only. feat-022 lays the body out in sections per
 * type, by `type.appearance`: «Описание» and the type's texts on the left,
 * «Планирование», «Трудозатраты (часы)» or «Классификация» on the right, and
 * «Связанная работа» last there, as mod-015 § 10.3 places it.
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
 * Loading (feat-022 spec § States): band and strip skeletons, and a
 * `FormSkeleton` in each column of the «Детали» grid — the type, and so its
 * sections, is not known yet. The second is hidden from assistive technology:
 * one «Загрузка данных» is announced for the page.
 */
function FormLoading() {
  const wide = useWideForm();
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
      <Box sx={gridSx(wide)}>
        <FormSkeleton fields={1} />
        <Box aria-hidden="true">
          <FormSkeleton fields={3} />
        </Box>
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
  const wide = useWideForm();
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
  const dirty = isDirty(item, draft);
  const busy = save.isPending;
  const layout = layoutOf(item.type.appearance);
  const formFields: readonly RefusalField[] = [...FORM_FIELDS, ...fieldsOf(item.type.appearance)];

  function edit(field: RefusalField, patch: Partial<WorkItemDraft>) {
    setEdited((current) => ({ ...(current ?? draftOf(item)), ...patch }));
    setErrors((current) => ({ ...current, [field]: undefined }));
  }

  /**
   * One field per type edited: its error clears; «Дата начала» clears
   * «Целевая дата»'s too, where the pair's order is reported (BR-M3-35).
   */
  function editField(name: WorkItemFieldName, value: string) {
    setEdited((current) => {
      const base = current ?? draftOf(item);
      return { ...base, fields: { ...base.fields, [name]: value } };
    });
    setErrors((current) => ({
      ...current,
      [name]: undefined,
      ...(name === 'startDate' ? { targetDate: undefined } : {}),
    }));
  }

  async function handleSave(event: FormEvent) {
    event.preventDefault();
    if (busy || !dirty) {
      return;
    }
    // The browser's checks: nothing is sent while one stands.
    const refused: FieldErrors = draftErrors(item, draft);
    if (Object.keys(refused).length > 0) {
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
      const placed = placeRefusal(error, formFields);
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
          disabled={busy || !dirty}
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
      <Box sx={gridSx(wide)}>
        <Stack spacing={3} useFlexGap sx={{ minWidth: 0 }}>
          <FormSection title="Описание">
            <TextField
              label="Описание"
              placeholder="Добавьте описание"
              multiline
              minRows={6}
              fullWidth
              value={draft.description}
              disabled={busy}
              error={errors.description !== undefined}
              helperText={errors.description ?? TEXT_HELPER}
              onChange={(event) => edit('description', { description: event.target.value })}
            />
          </FormSection>
          {layout.texts.map((name) => (
            <FormSection key={name} title={TEXT_FIELDS[name].label}>
              <TextField
                label={TEXT_FIELDS[name].label}
                placeholder={TEXT_FIELDS[name].placeholder}
                multiline
                minRows={6}
                fullWidth
                value={draft.fields[name]}
                disabled={busy}
                error={errors[name] !== undefined}
                helperText={errors[name] ?? TEXT_HELPER}
                onChange={(event) => editField(name, event.target.value)}
              />
            </FormSection>
          ))}
        </Stack>
        <Stack spacing={3} useFlexGap sx={{ minWidth: 0 }}>
          {layout.sections.map((section) => (
            <FormSection key={section.title} title={section.title}>
              <Stack
                spacing={2}
                useFlexGap
                sx={{ maxWidth: wide ? undefined : theme.layout.formColumnWidth }}
              >
                {section.fields.map((name) => (
                  <FieldControl
                    key={name}
                    name={name}
                    value={draft.fields[name]}
                    disabled={busy}
                    error={errors[name]}
                    onChange={(value) => editField(name, value)}
                  />
                ))}
              </Stack>
            </FormSection>
          ))}
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
        </Stack>
      </Box>
      {notice}
    </Box>
  );
}
