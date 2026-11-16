import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import ContentCopyOutlinedIcon from '@mui/icons-material/ContentCopyOutlined';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import PlaylistAddCheckIcon from '@mui/icons-material/PlaylistAddCheck';
import SaveOutlinedIcon from '@mui/icons-material/SaveOutlined';
import SearchOffIcon from '@mui/icons-material/SearchOff';
import Box from '@mui/material/Box';
import { ApiError } from '../../../shared/api/httpClient';
import { importErrorCodes } from '../../../shared/api/errorCodes';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import {
  AccessDenied,
  BoxedTabPanel,
  BoxedTabs,
  ConfirmDialog,
  ErrorBanner,
  FieldsSkeleton,
  InlineNotice,
  InlineTextField,
  StateFrame,
  useNotice,
} from '../../../shared/ui';
import { PageHeader } from '../../../shared/ui/PageHeader';
import type { TemplateDto } from '../api/importsApi';
import { IdentifierTab } from '../components/IdentifierTab';
import { MappingTab } from '../components/MappingTab';
import { SourceTab, type SampleFile } from '../components/SourceTab';
import { ValuesTab } from '../components/ValuesTab';
import {
  useCopyTemplate,
  useCreateTemplate,
  useDeleteTemplate,
  useReplaceTemplate,
} from '../hooks/mutations';
import { useCatalogue, useTemplate, useTemplates } from '../hooks/queries';
import {
  draftFromTemplate,
  emptyDraft,
  hasErrors,
  tabsWithErrors,
  toInput,
  validate,
  type Draft,
  type DraftErrors,
  type EditorTab,
} from '../lib/draft';
import { heldSample } from '../lib/heldSample';
import { saveFailure } from '../lib/serverErrors';
import { texts } from '../lib/texts';
import { checkPath, importsPath, templatePath } from '../paths';

const TAB_ORDER: EditorTab[] = ['source', 'mapping', 'values', 'identifier'];
const ID_BASE = 'import-template';

function codeOf(error: unknown): string | undefined {
  return error instanceof ApiError ? error.errorCode : undefined;
}

/** S2 and S2b — one page for a new template (`/templates/new`) and a saved one (`/templates/:t`). */
export function TemplateEditorPage() {
  const { organizationId = '', templateId } = useParams<{
    organizationId: string;
    templateId: string;
  }>();
  const isNew = templateId === undefined;
  const template = useTemplate(organizationId, templateId ?? '', !isNew);
  // A new template's right to exist is the list's page-level flag: the editor asks nothing else.
  const list = useTemplates(organizationId, { page: 1, pageSize: 1 }, isNew);

  const toList = { label: texts.toList, to: importsPath(organizationId) };

  if (isNew) {
    if (list.isPending) {
      return (
        <>
          <PageHeader title={texts.newTemplate} />
          <FieldsSkeleton count={4} />
        </>
      );
    }
    if (list.isError) {
      return (
        <>
          <PageHeader title={texts.newTemplate} />
          <ErrorBanner
            error={list.error}
            onRetry={() => void list.refetch()}
            title={texts.loadTemplateFailed}
          />
        </>
      );
    }
    if (!list.data.canCreate) return <AccessDenied />;
    return <TemplateEditor key="new" organizationId={organizationId} template={undefined} />;
  }

  if (template.isPending) {
    return (
      <>
        <PageHeader title={texts.newTemplate} />
        <FieldsSkeleton count={4} />
      </>
    );
  }
  if (template.isError) {
    const code = codeOf(template.error);
    if (code === importErrorCodes.templateNotFound) {
      return (
        <StateFrame
          fullPage
          icon={SearchOffIcon}
          heading={texts.templateNotFound}
          hint={texts.templateNotFoundHint}
          action={toList}
        />
      );
    }
    if (code === importErrorCodes.accessForbidden) return <AccessDenied />;
    return (
      <>
        <PageHeader title={texts.newTemplate} />
        <ErrorBanner
          error={template.error}
          onRetry={() => void template.refetch()}
          title={texts.loadTemplateFailed}
        />
      </>
    );
  }
  return (
    <TemplateEditor
      key={`${template.data.id}:${template.data.version}`}
      organizationId={organizationId}
      template={template.data}
    />
  );
}

function TemplateEditor({
  organizationId,
  template,
}: {
  organizationId: string;
  template: TemplateDto | undefined;
}) {
  const navigate = useNavigate();
  const notice = useNotice();
  const catalogueQuery = useCatalogue(organizationId);
  const create = useCreateTemplate(organizationId);
  const replace = useReplaceTemplate(organizationId, template?.id ?? '');
  const copy = useCopyTemplate(organizationId);
  const remove = useDeleteTemplate(organizationId);

  const [initial, setInitial] = useState<Draft>(() =>
    template ? draftFromTemplate(template) : emptyDraft(),
  );
  const [draft, setDraft] = useState<Draft>(initial);
  const sampleKey = heldSample.keyOf(template?.id);
  const [sample, setSampleState] = useState<SampleFile | null>(() => heldSample.get(sampleKey));
  const setSample = (next: SampleFile | null) => {
    heldSample.set(sampleKey, next);
    setSampleState(next);
  };
  const [tab, setTab] = useState<EditorTab>('source');
  const [shown, setShown] = useState<DraftErrors | null>(null);
  const [banner, setBanner] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const readOnly = template !== undefined && !template.canEdit;
  const dirty = JSON.stringify(draft) !== JSON.stringify(initial);
  const saving = create.isPending || replace.isPending;
  const catalogue = catalogueQuery.data;
  const errors = shown ?? { mappings: {}, missingRequired: [], recoding: {} };
  const counts = tabsWithErrors(errors);
  const noFile = draft.source.headers.length === 0;

  function onSaved(saved: TemplateDto, createdNow: boolean) {
    setBanner(null);
    setShown(null);
    notice.show(texts.saved);
    if (createdNow) {
      heldSample.promote(saved.id);
      navigate(templatePath(organizationId, saved.id), { replace: true });
    } else {
      setInitial(draft);
    }
  }

  function fail(error: unknown) {
    const failure = saveFailure(error, draft);
    setShown(failure.errors);
    setBanner(failure.banner ?? (hasErrors(failure.errors) ? texts.notSaved : apiErrorText(error)));
    const first = TAB_ORDER.find((candidate) => tabsWithErrors(failure.errors)[candidate] > 0);
    if (first) setTab(first);
  }

  function save() {
    if (saving || readOnly) return;
    const found = validate(draft, catalogue);
    if (hasErrors(found)) {
      setShown(found);
      setBanner(texts.notSaved);
      const first = TAB_ORDER.find((candidate) => tabsWithErrors(found)[candidate] > 0);
      if (first) setTab(first);
      return;
    }
    setShown(null);
    setBanner(null);
    if (template) {
      replace.mutate(toInput(draft), {
        onSuccess: (saved) => onSaved(saved, false),
        onError: fail,
      });
    } else {
      create.mutate(toInput(draft), { onSuccess: (saved) => onSaved(saved, true), onError: fail });
    }
  }

  function copyThis() {
    if (!template) return;
    copy.mutate(template.id, {
      onSuccess: (created) => {
        notice.show(texts.copied(created.name));
        navigate(templatePath(organizationId, created.id));
      },
      onError: (error) => notice.show(apiErrorText(error), 'error'),
    });
  }

  function confirmDelete() {
    if (!template) return;
    setDeleteError(null);
    remove.mutate(template.id, {
      onSuccess: () => {
        notice.show(texts.deleted);
        navigate(importsPath(organizationId));
      },
      onError: (error) => {
        if (codeOf(error) === importErrorCodes.templateNotFound) {
          notice.show(texts.alreadyDeleted);
          navigate(importsPath(organizationId));
        } else {
          setDeleteError(apiErrorText(error));
        }
      },
    });
  }

  const commands = [
    ...(template?.canRun
      ? [
          {
            label: texts.checkFile,
            icon: <PlaylistAddCheckIcon />,
            to: dirty ? undefined : checkPath(organizationId, template.id),
            onClick: dirty ? undefined : undefined,
            disabled: dirty,
          },
        ]
      : []),
    ...(template?.canCopy
      ? [{ label: texts.copyCommand, icon: <ContentCopyOutlinedIcon />, onClick: copyThis }]
      : []),
    ...(template?.canDelete
      ? [
          {
            label: texts.deleteCommand,
            icon: <DeleteOutlineOutlinedIcon />,
            onClick: () => {
              setDeleteError(null);
              setDeleting(true);
            },
          },
        ]
      : []),
  ];

  const title = template?.name ?? (draft.name.trim() || texts.newTemplate);

  function panel(which: EditorTab) {
    if (!catalogue) return null;
    if (which !== 'source' && noFile) {
      return (
        <StateFrame
          compact
          icon={InboxOutlinedIcon}
          heading={texts.uploadFirst}
          hint={texts.uploadFirstHint}
          action={{ label: texts.toSource, onClick: () => setTab('source') }}
        />
      );
    }
    switch (which) {
      case 'source':
        return (
          <SourceTab
            organizationId={organizationId}
            draft={draft}
            onDraft={setDraft}
            sample={sample}
            onSample={setSample}
            errors={errors}
            readOnly={readOnly}
          />
        );
      case 'mapping':
        return (
          <MappingTab
            organizationId={organizationId}
            draft={draft}
            onDraft={setDraft}
            catalogue={catalogue}
            sample={sample}
            errors={errors}
            readOnly={readOnly}
            onJumpToValues={() => setTab('values')}
          />
        );
      case 'values':
        return (
          <ValuesTab
            organizationId={organizationId}
            draft={draft}
            onDraft={setDraft}
            catalogue={catalogue}
            sample={sample}
            errors={errors}
            readOnly={readOnly}
          />
        );
      case 'identifier':
        return <IdentifierTab draft={draft} onDraft={setDraft} readOnly={readOnly} />;
    }
  }

  return (
    <>
      <PageHeader
        title={title}
        primaryAction={
          readOnly ? undefined : { label: texts.save, icon: <SaveOutlinedIcon />, onClick: save }
        }
        commands={commands}
      />
      {dirty && template?.canRun && (
        <Box sx={{ mb: 1 }}>
          <InlineNotice severity="info">{texts.saveFirst}</InlineNotice>
        </Box>
      )}
      {readOnly && template && (
        <Box sx={{ mb: 2 }}>
          <InlineNotice severity="info">
            {texts.readOnlyNote(template.authorName ?? texts.unknownAuthor)}
          </InlineNotice>
        </Box>
      )}
      {banner && (
        <Box sx={{ mb: 2 }}>
          <InlineNotice severity="error">{banner}</InlineNotice>
        </Box>
      )}
      <Box sx={{ mb: 2 }}>
        <InlineTextField
          label={texts.nameLabel}
          frame="always"
          required
          maxLength={100}
          value={draft.name}
          readOnly={readOnly}
          error={errors.name}
          onChange={(name) => setDraft({ ...draft, name })}
        />
      </Box>
      {catalogueQuery.isPending ? (
        <FieldsSkeleton count={3} />
      ) : catalogueQuery.isError ? (
        <ErrorBanner
          error={catalogueQuery.error}
          onRetry={() => void catalogueQuery.refetch()}
          title={texts.loadTemplateFailed}
        />
      ) : (
        <>
          <BoxedTabs
            idBase={ID_BASE}
            aria-label={texts.tabsLabel}
            value={tab}
            onChange={(value) => setTab(value as EditorTab)}
            tabs={TAB_ORDER.map((value) => ({
              value,
              label: texts.tabs[value],
              count: counts[value] > 0 ? counts[value] : undefined,
            }))}
          />
          {TAB_ORDER.map((value) => (
            <BoxedTabPanel key={value} idBase={ID_BASE} value={value} selected={tab === value}>
              <Box sx={{ pt: 2 }}>{panel(value)}</Box>
            </BoxedTabPanel>
          ))}
        </>
      )}
      {deleting && template && (
        <ConfirmDialog
          open
          destructive
          title={texts.deleteTitle}
          confirmLabel={texts.delete}
          busy={remove.isPending}
          error={deleteError ?? undefined}
          onConfirm={confirmDelete}
          onClose={() => setDeleting(false)}
        >
          {texts.deleteText(template.name)}
        </ConfirmDialog>
      )}
    </>
  );
}
