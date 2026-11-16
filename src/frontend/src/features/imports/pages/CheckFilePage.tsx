import { useRef, useState } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import FileDownloadOutlinedIcon from '@mui/icons-material/FileDownloadOutlined';
import FactCheckOutlinedIcon from '@mui/icons-material/FactCheckOutlined';
import UploadFileOutlinedIcon from '@mui/icons-material/UploadFileOutlined';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { ApiError } from '../../../shared/api/httpClient';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import {
  AccessDenied,
  ActionButton,
  DataTable,
  ErrorBanner,
  FieldsSkeleton,
  FilePickerButton,
  IconAction,
  InlineNotice,
  InlineSelectField,
  StateFrame,
  Tag,
  useNotice,
  type DataColumn,
} from '../../../shared/ui';
import { PageHeader } from '../../../shared/ui/PageHeader';
import type { DryRunRow, ResultFilter } from '../api/importsApi';
import { downloadDryRunReport } from '../api/importsApi';
import { useStartDryRun, useUploadFile } from '../hooks/mutations';
import { useDryRun, useTemplates } from '../hooks/queries';
import { catalogText } from '../lib/catalogText';
import { saveBlob } from '../lib/download';
import { isFileRefusal, refusalText } from '../lib/refusal';
import { texts } from '../lib/texts';

const FILTERS: ResultFilter[] = ['all', 'created', 'updated', 'unchanged', 'skipped', 'error'];

function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

/** S3 — «Проверка файла»: a dry run of a file by a template; nothing is written (design § 6). */
export function CheckFilePage() {
  const { organizationId = '' } = useParams<{ organizationId: string }>();
  const [params] = useSearchParams();
  const notice = useNotice();
  const templates = useTemplates(organizationId, { pageSize: 100, sort: 'name' });
  const upload = useUploadFile(organizationId);
  const start = useStartDryRun(organizationId);
  const abort = useRef<AbortController | null>(null);

  const [templateId, setTemplateId] = useState(params.get('template') ?? '');
  const [file, setFile] = useState<File | null>(null);
  const [dryRunId, setDryRunId] = useState<string | null>(null);
  const [result, setResult] = useState<ResultFilter>('all');
  const [running, setRunning] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [failure, setFailure] = useState<unknown>(null);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const [downloading, setDownloading] = useState(false);

  const run = useDryRun(organizationId, dryRunId, result);
  const report = run.data;

  if (templates.isPending) {
    return (
      <>
        <PageHeader title={texts.checkTitle} />
        <FieldsSkeleton count={3} />
      </>
    );
  }
  if (templates.isError) {
    return (
      <>
        <PageHeader title={texts.checkTitle} />
        <ErrorBanner error={templates.error} onRetry={() => void templates.refetch()} />
      </>
    );
  }
  if (!templates.data.canRun) return <AccessDenied />;

  const runnable = templates.data.items.filter((item) => item.canRun);
  const known = runnable.some((item) => item.id === templateId);
  const missing = templateId !== '' && !known;
  const canStart = known && file !== null && !running;

  async function check() {
    if (!file || !known) return;
    const controller = new AbortController();
    abort.current = controller;
    setRunning(true);
    setRefusal(null);
    setFailure(null);
    setDryRunId(null);
    setExpanded(new Set());
    setResult('all');
    try {
      const uploaded = await upload.mutateAsync({ file });
      const done = await start.mutateAsync({
        templateId,
        fileId: uploaded.fileId,
        signal: controller.signal,
      });
      setDryRunId(done.id);
    } catch (error) {
      if (isAbort(error)) return;
      if (isFileRefusal(error)) setRefusal(refusalText(error));
      else setFailure(error);
    } finally {
      setRunning(false);
      abort.current = null;
    }
  }

  async function download() {
    if (!dryRunId) return;
    setDownloading(true);
    try {
      const { blob, fileName } = await downloadDryRunReport(organizationId, dryRunId);
      saveBlob(blob, fileName ?? texts.reportFile);
    } catch (error) {
      notice.show(`${texts.reportFailed}. ${apiErrorText(error)}`, 'error');
    } finally {
      setDownloading(false);
    }
  }

  function toggle(row: number) {
    setExpanded((current) => {
      const next = new Set(current);
      if (next.has(row)) next.delete(row);
      else next.add(row);
      return next;
    });
  }

  const columns: DataColumn<DryRunRow>[] = [
    {
      key: 'row',
      header: texts.rowCol,
      width: 96,
      render: (row) => (
        <Box sx={{ display: 'flex', alignItems: 'center' }}>
          <IconAction
            size="row"
            label={expanded.has(row.row) ? texts.collapseRow(row.row) : texts.expandRow(row.row)}
            icon={expanded.has(row.row) ? <ExpandLessIcon /> : <ExpandMoreIcon />}
            onClick={() => toggle(row.row)}
          />
          {row.row}
        </Box>
      ),
    },
    {
      key: 'result',
      header: texts.resultCol,
      width: 160,
      render: (row) => (
        <Tag
          label={texts.results[row.result]}
          tone={
            row.result === 'error'
              ? 'warning'
              : row.result === 'created' || row.result === 'updated'
                ? 'success'
                : 'neutral'
          }
        />
      ),
    },
    {
      key: 'item',
      header: texts.itemCol,
      render: (row) => (
        <Stack spacing={0.5}>
          <span>{[row.item?.identifier, row.item?.title].filter(Boolean).join(' ') || '—'}</span>
          {expanded.has(row.row) && <RowDetail row={row} />}
        </Stack>
      ),
    },
    {
      key: 'project',
      header: texts.projectCol,
      render: (row) => row.project?.name ?? '—',
    },
    {
      key: 'reasons',
      header: texts.reasonsCol,
      render: (row) => (
        <Stack spacing={0.25}>
          {row.reasons.slice(0, expanded.has(row.row) ? undefined : 1).map((reason, index) => (
            <span key={`${reason.code}-${index}`}>{reason.text}</span>
          ))}
          {!expanded.has(row.row) && row.reasons.length > 1 && (
            <Typography variant="caption" sx={{ color: 'text.secondary' }}>
              {texts.moreReasons(row.reasons.length - 1)}
            </Typography>
          )}
        </Stack>
      ),
    },
  ];

  const empty = report !== undefined && report.totals.all === 0;

  return (
    <>
      <PageHeader title={texts.checkTitle} />
      <Stack spacing={2} sx={{ maxWidth: 720, mb: 3 }}>
        <InlineSelectField
          label={texts.template}
          frame="always"
          value={known ? templateId : ''}
          emptyLabel={texts.pickTemplate}
          options={runnable.map((item) => ({ value: item.id, label: item.name }))}
          onChange={(value) => setTemplateId(value)}
        />
        {missing && <InlineNotice severity="error">{texts.checkTemplateMissing}</InlineNotice>}
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, flexWrap: 'wrap' }}>
          <FilePickerButton
            label={texts.pickFile}
            icon={<UploadFileOutlinedIcon />}
            accept=".xlsx,.csv"
            tone="neutral"
            disabled={running}
            onPick={(picked) => {
              setFile(picked);
              setRefusal(null);
            }}
          />
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {file ? file.name : texts.fileHint}
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <ActionButton
            label={texts.run}
            icon={<FactCheckOutlinedIcon />}
            disabled={!canStart}
            busy={running}
            onClick={() => void check()}
          />
          {running && (
            <ActionButton
              label={texts.cancelRun}
              tone="subtle"
              onClick={() => abort.current?.abort()}
            />
          )}
        </Box>
        {running && (
          <Box role="status">
            <Typography variant="body2" sx={{ mb: 1 }}>
              {texts.running}
            </Typography>
            <FieldsSkeleton count={1} />
          </Box>
        )}
        {refusal && (
          <InlineNotice severity="error">{`${texts.fileRefused}. ${refusal}`}</InlineNotice>
        )}
        {failure !== null && (
          <ErrorBanner
            error={failure instanceof ApiError || failure instanceof Error ? failure : new Error()}
            title={texts.runFailed}
            onRetry={() => void check()}
          />
        )}
      </Stack>

      {!running && report === undefined && failure === null && refusal === null && (
        <StateFrame
          compact
          icon={FactCheckOutlinedIcon}
          heading={texts.runHeading}
          hint={texts.runHint}
        />
      )}

      {report && (
        <Stack spacing={2}>
          {report.stale && <InlineNotice severity="warning">{texts.stale}</InlineNotice>}
          {report.notes.map((note) => (
            <InlineNotice key={note} severity="info">
              {catalogText(note, {})}
            </InlineNotice>
          ))}
          {!report.importable && !report.stale && !empty && (
            <InlineNotice severity="warning">{texts.nothingToImport}</InlineNotice>
          )}
          <Box
            role="group"
            aria-label={texts.totalsLabel}
            sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}
          >
            {FILTERS.map((filter) => (
              <ActionButton
                key={filter}
                label={`${texts.results[filter]}: ${report.totals[filter]}`}
                tone={result === filter ? 'primary' : 'neutral'}
                aria-pressed={result === filter}
                onClick={() => setResult(filter)}
              />
            ))}
            <ActionButton
              label={texts.downloadReport}
              icon={<FileDownloadOutlinedIcon />}
              tone="subtle"
              busy={downloading}
              onClick={() => void download()}
            />
          </Box>
          <DataTable
            aria-label={texts.previewTable}
            columns={columns}
            rows={report.rows}
            rowKey={(row) => String(row.row)}
            loading={run.isFetching && run.isPlaceholderData}
            empty={{
              icon: FactCheckOutlinedIcon,
              heading: texts.noRowsOfResult,
            }}
          />
          <Typography variant="caption" sx={{ color: 'text.secondary' }}>
            {texts.previewCaption}
          </Typography>
        </Stack>
      )}
    </>
  );
}

function RowDetail({ row }: { row: DryRunRow }) {
  if (row.fields.length === 0) return null;
  const update = row.result === 'updated';
  return (
    <Box
      component="dl"
      sx={{ m: 0, display: 'grid', gridTemplateColumns: 'max-content 1fr', columnGap: 2 }}
    >
      {row.fields.map((field) => (
        <Box key={field.field} sx={{ display: 'contents' }}>
          <Typography component="dt" variant="caption" sx={{ color: 'text.secondary' }}>
            {field.name}
          </Typography>
          <Typography component="dd" variant="body2" sx={{ m: 0 }}>
            {update && field.was !== null
              ? `${texts.was}: ${field.was} → ${texts.became}: ${field.value ?? '—'}`
              : (field.value ?? '—')}
          </Typography>
        </Box>
      ))}
    </Box>
  );
}
