import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import AddIcon from '@mui/icons-material/Add';
import ContentCopyOutlinedIcon from '@mui/icons-material/ContentCopyOutlined';
import DeleteOutlineOutlinedIcon from '@mui/icons-material/DeleteOutlineOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import PlaylistAddCheckIcon from '@mui/icons-material/PlaylistAddCheck';
import ViewColumnOutlinedIcon from '@mui/icons-material/ViewColumnOutlined';
import VisibilityOutlinedIcon from '@mui/icons-material/VisibilityOutlined';
import Box from '@mui/material/Box';
import { ApiError } from '../../../shared/api/httpClient';
import { importErrorCodes } from '../../../shared/api/errorCodes';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { formatDateTime } from '../../../shared/lib/format';
import {
  ConfirmDialog,
  DataTable,
  IconAction,
  InlineNotice,
  InlineSelectField,
  InlineTextField,
  MoreActionsMenu,
  TruncatedText,
  useNotice,
  type DataColumn,
  type HeadCellSortDirection,
} from '../../../shared/ui';
import { FilterBar } from '../../../shared/ui/FilterBar';
import { PageHeader } from '../../../shared/ui/PageHeader';
import type { TemplateListItem, TemplatesSort } from '../api/importsApi';
import { ExportColumnsDialog } from '../components/ExportColumnsDialog';
import { useCopyTemplate, useDeleteTemplate } from '../hooks/mutations';
import { useTemplates } from '../hooks/queries';
import { texts } from '../lib/texts';
import { checkPath, newTemplatePath, templatePath } from '../paths';

const PAGE_SIZE = 10;

type SortColumn = 'name' | 'author' | 'updatedAt';

function sortKey(column: SortColumn, descending: boolean): TemplatesSort {
  return (descending ? `-${column}` : column) as TemplatesSort;
}

function errorCodeOf(error: unknown): string | undefined {
  return error instanceof ApiError ? error.errorCode : undefined;
}

function sourceText(template: TemplateListItem): string {
  const { source } = template;
  return source.format === 'xlsx'
    ? texts.sourceXlsx(source.sheet)
    : texts.sourceCsv(source.encoding, source.delimiter);
}

/**
 * S1 — «Импорт»: the organization's import templates (mod-024 design § 2). The rights are the
 * server's: the page-level `canCreate` / `canRun` and each row's `canEdit` / `canCopy` / `canDelete` /
 * `canRun`; the page keeps no copy of the rule (BR-IE-18). A member with the read code only sees the
 * rows and «Столбцы».
 */
export function TemplatesListPage() {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const navigate = useNavigate();
  const notice = useNotice();
  const [name, setName] = useState('');
  const [author, setAuthor] = useState<'all' | 'mine'>('all');
  const [sort, setSort] = useState<{ column: SortColumn; descending: boolean }>({
    column: 'name',
    descending: false,
  });
  const [page, setPage] = useState(0);
  const [limitNotice, setLimitNotice] = useState(false);
  const [deleting, setDeleting] = useState<TemplateListItem | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [columnsOf, setColumnsOf] = useState<TemplateListItem | null>(null);

  const query = useTemplates(organizationId, {
    name: name.trim() || undefined,
    author,
    sort: sortKey(sort.column, sort.descending),
    page: page + 1,
    pageSize: PAGE_SIZE,
  });
  const copy = useCopyTemplate(organizationId);
  const remove = useDeleteTemplate(organizationId);
  const list = query.data;
  const filtered = name.trim() !== '' || author !== 'all';

  function sortOf(column: SortColumn) {
    const direction: HeadCellSortDirection =
      sort.column === column ? (sort.descending ? 'desc' : 'asc') : false;
    return {
      direction,
      onSort: () => {
        setSort((current) => ({
          column,
          descending: current.column === column ? !current.descending : false,
        }));
        setPage(0);
      },
    };
  }

  function clearFilters() {
    setName('');
    setAuthor('all');
    setPage(0);
  }

  function copyTemplate(template: TemplateListItem) {
    setLimitNotice(false);
    copy.mutate(template.id, {
      onSuccess: (created) => notice.show(texts.copied(created.name)),
      onError: (error) => {
        if (errorCodeOf(error) === importErrorCodes.templateLimitReached) setLimitNotice(true);
        else notice.show(apiErrorText(error), 'error');
      },
    });
  }

  function confirmDelete() {
    if (!deleting) return;
    setDeleteError(null);
    remove.mutate(deleting.id, {
      onSuccess: () => {
        setDeleting(null);
        notice.show(texts.deleted);
      },
      onError: (error) => {
        if (errorCodeOf(error) === importErrorCodes.templateNotFound) {
          setDeleting(null);
          notice.show(texts.alreadyDeleted);
        } else {
          setDeleteError(apiErrorText(error));
        }
      },
    });
  }

  const columns: DataColumn<TemplateListItem>[] = [
    {
      key: 'name',
      header: texts.columns.name,
      sort: sortOf('name'),
      render: (template) =>
        template.canEdit || template.canRun ? (
          <TruncatedText to={templatePath(organizationId, template.id)} look="link">
            {template.name}
          </TruncatedText>
        ) : (
          <TruncatedText>{template.name}</TruncatedText>
        ),
    },
    { key: 'source', header: texts.columns.source, render: (template) => sourceText(template) },
    {
      key: 'author',
      header: texts.columns.author,
      sort: sortOf('author'),
      render: (template) => template.authorName ?? texts.noAuthor,
    },
    {
      key: 'changed',
      header: texts.columns.changed,
      sort: sortOf('updatedAt'),
      render: (template) => formatDateTime(template.updatedAt),
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      width: 168,
      render: (template) => (
        <Box sx={{ display: 'flex', justifyContent: 'flex-end', alignItems: 'center' }}>
          {template.canEdit ? (
            <IconAction
              size="row"
              label={texts.edit(template.name)}
              icon={<EditOutlinedIcon />}
              onClick={() => navigate(templatePath(organizationId, template.id))}
            />
          ) : template.canRun ? (
            <IconAction
              size="row"
              label={texts.open(template.name)}
              icon={<VisibilityOutlinedIcon />}
              onClick={() => navigate(templatePath(organizationId, template.id))}
            />
          ) : (
            <IconAction
              size="row"
              label={texts.columnsOf + ': ' + template.name}
              icon={<ViewColumnOutlinedIcon />}
              onClick={() => setColumnsOf(template)}
            />
          )}
          {template.canCopy && (
            <IconAction
              size="row"
              label={texts.copy(template.name)}
              icon={<ContentCopyOutlinedIcon />}
              busy={copy.isPending && copy.variables === template.id}
              onClick={() => copyTemplate(template)}
            />
          )}
          {template.canDelete && (
            <IconAction
              size="row"
              label={texts.remove(template.name)}
              icon={<DeleteOutlineOutlinedIcon />}
              onClick={() => {
                setDeleteError(null);
                setDeleting(template);
              }}
            />
          )}
          {template.canRun && (
            <MoreActionsMenu
              label={texts.moreActions(template.name)}
              size="row"
              items={[
                {
                  label: texts.checkByTemplate,
                  icon: <PlaylistAddCheckIcon />,
                  to: checkPath(organizationId, template.id),
                },
              ]}
            />
          )}
        </Box>
      ),
    },
  ];

  const canCreate = list?.canCreate ?? false;
  const loading = query.isPending || (query.isError && query.isFetching);

  return (
    <>
      <PageHeader
        title={texts.listTitle}
        primaryAction={
          canCreate
            ? {
                label: texts.createTemplate,
                icon: <AddIcon />,
                to: newTemplatePath(organizationId),
              }
            : undefined
        }
        commands={
          list?.canRun
            ? [
                {
                  label: texts.checkFile,
                  icon: <PlaylistAddCheckIcon />,
                  to: checkPath(organizationId),
                },
              ]
            : []
        }
      />
      <FilterBar active={filtered} onClear={clearFilters}>
        <InlineTextField
          label={texts.filterName}
          labelPlacement="hidden"
          placeholder={texts.filterName}
          frame="always"
          value={name}
          onChange={(value) => {
            setName(value);
            setPage(0);
          }}
        />
        <InlineSelectField
          label={texts.filterAuthor}
          frame="always"
          value={author}
          options={[
            { value: 'all', label: `${texts.filterAuthor}: ${texts.authorAll}` },
            { value: 'mine', label: `${texts.filterAuthor}: ${texts.authorMine}` },
          ]}
          onChange={(value) => {
            setAuthor(value === 'mine' ? 'mine' : 'all');
            setPage(0);
          }}
        />
      </FilterBar>
      {limitNotice && (
        <Box sx={{ mb: 2 }}>
          <InlineNotice severity="warning">{texts.limitReached}</InlineNotice>
        </Box>
      )}
      <DataTable
        aria-label={texts.tableLabel}
        columns={columns}
        rows={list?.items ?? []}
        rowKey={(template) => template.id}
        loading={loading}
        error={query.isError && !query.isFetching ? apiErrorText(query.error) : undefined}
        onRetry={() => void query.refetch()}
        empty={
          filtered
            ? {
                icon: FilterListOffIcon,
                heading: texts.nothingFound,
                hint: texts.nothingFoundHint,
                action: { label: texts.resetFilter, onClick: clearFilters },
              }
            : {
                icon: InboxOutlinedIcon,
                heading: texts.emptyHeading,
                hint: canCreate ? texts.emptyHintCreate : texts.emptyHintOther,
                action: canCreate
                  ? {
                      label: texts.createTemplate,
                      icon: <AddIcon />,
                      onClick: () => navigate(newTemplatePath(organizationId)),
                    }
                  : undefined,
              }
        }
        pager={
          list && list.total > PAGE_SIZE
            ? {
                page,
                rowsPerPage: PAGE_SIZE,
                count: list.total,
                onPageChange: setPage,
                disabled: query.isFetching,
              }
            : undefined
        }
      />
      {deleting && (
        <ConfirmDialog
          open
          destructive
          title={texts.deleteTitle}
          confirmLabel={texts.delete}
          busy={remove.isPending}
          error={deleteError ?? undefined}
          onConfirm={confirmDelete}
          onClose={() => setDeleting(null)}
        >
          {texts.deleteText(deleting.name)}
        </ConfirmDialog>
      )}
      {columnsOf && (
        <ExportColumnsDialog
          organizationId={organizationId}
          template={columnsOf}
          onClose={() => setColumnsOf(null)}
        />
      )}
    </>
  );
}
