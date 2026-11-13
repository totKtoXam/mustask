import { useState } from 'react';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { ApiError } from '../../../shared/api/httpClient';
import { importErrorCodes } from '../../../shared/api/errorCodes';
import {
  BodyRow,
  Cell,
  FilePickerButton,
  HeadCell,
  IconAction,
  InlineNotice,
  InlineNumberField,
  InlineSelectField,
  Surface,
  TableCard,
  ThinCloseIcon,
  FieldsSkeleton,
} from '../../../shared/ui';
import type { FileRead, FileSourceOptions, UploadedFile } from '../api/importsApi';
import { useReadFileColumns, useUploadFile } from '../hooks/mutations';
import { applyFileRead, type Draft, type DraftErrors } from '../lib/draft';
import { refusalText } from '../lib/refusal';
import { texts } from '../lib/texts';

/** The sample file held for this editing session (its stored copy lives at most 24 hours). */
export interface SampleFile {
  fileId: string;
  fileName: string;
  size: number;
  expiresAt: string;
  read: FileRead;
}

export function sampleOf(uploaded: UploadedFile): SampleFile {
  const { fileId, fileName, size, expiresAt, read } = uploaded;
  return { fileId, fileName, size, expiresAt, read };
}

function sizeText(bytes: number): string {
  return bytes >= 1024 * 1024
    ? `${(bytes / (1024 * 1024)).toFixed(1)} МБ`
    : `${Math.max(1, Math.round(bytes / 1024))} КБ`;
}

const ENCODINGS = [
  { value: 'utf-8', label: 'UTF-8' },
  { value: 'windows-1251', label: 'Windows-1251' },
];
const DELIMITERS = [
  { value: ';', label: ';' },
  { value: ',', label: ',' },
  { value: '\t', label: texts.tabulation },
];

/**
 * Tab «Источник» (design § 3.1): the sample file, the sheet, the rows, the CSV options and a preview of
 * the headers with the first rows. A refused file leaves the template untouched.
 */
export function SourceTab({
  organizationId,
  draft,
  onDraft,
  sample,
  onSample,
  errors,
  readOnly,
}: {
  organizationId: string;
  draft: Draft;
  onDraft: (draft: Draft) => void;
  sample: SampleFile | null;
  onSample: (sample: SampleFile | null) => void;
  errors: DraftErrors;
  readOnly: boolean;
}) {
  const upload = useUploadFile(organizationId);
  const reread = useReadFileColumns(organizationId);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [headerProblem, setHeaderProblem] = useState<string | null>(null);
  const busy = upload.isPending || reread.isPending;
  const read = sample?.read ?? null;
  const { source } = draft;

  function fail(error: unknown) {
    if (error instanceof ApiError && error.errorCode === importErrorCodes.headerEmpty) {
      setHeaderProblem(texts.noHeaders);
      return;
    }
    setRefusal(refusalText(error));
  }

  function pick(file: File) {
    setRefusal(null);
    setHeaderProblem(null);
    upload.mutate(
      { file },
      {
        onSuccess: (uploaded) => {
          onSample(sampleOf(uploaded));
          onDraft(applyFileRead(draft, uploaded.read));
        },
        onError: fail,
      },
    );
  }

  function readAgain(options: FileSourceOptions, next: Draft) {
    onDraft(next);
    if (!sample) return;
    setRefusal(null);
    setHeaderProblem(null);
    reread.mutate(
      { fileId: sample.fileId, options },
      {
        onSuccess: (fileRead) => {
          onSample({ ...sample, read: fileRead });
          onDraft(applyFileRead(next, fileRead));
        },
        onError: fail,
      },
    );
  }

  function numberField(key: 'headerRow' | 'firstDataRow', text: string): Draft {
    const parsed = Number.parseInt(text, 10);
    return {
      ...draft,
      source: { ...source, [key]: Number.isNaN(parsed) ? 0 : parsed },
    };
  }

  const optionsOf = (next: Draft): FileSourceOptions => ({
    sheet: next.source.sheet,
    headerRow: next.source.headerRow,
    firstDataRow: next.source.firstDataRow,
    encoding: next.source.encoding,
    delimiter: next.source.delimiter,
  });

  return (
    <Stack spacing={2}>
      {!readOnly && (
        <Surface variant="card" padding="form">
          <Stack spacing={1} sx={{ alignItems: 'flex-start' }}>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
              <FilePickerButton
                label={texts.pickFile}
                accept=".xlsx,.csv"
                icon={<UploadFileIcon />}
                busy={upload.isPending}
                onPick={pick}
              />
              {sample && (
                <>
                  <Typography variant="body2">
                    {sample.fileName} · {sizeText(sample.size)}
                  </Typography>
                  <IconAction
                    label={texts.removeFile}
                    icon={<ThinCloseIcon />}
                    onClick={() => {
                      onSample(null);
                      setRefusal(null);
                    }}
                  />
                </>
              )}
            </Stack>
            <Typography variant="caption" sx={{ color: 'text.secondary' }}>
              {texts.fileHint}
            </Typography>
          </Stack>
        </Surface>
      )}
      {busy && (
        <Box role="status" aria-label={texts.uploading}>
          <FieldsSkeleton count={1} />
        </Box>
      )}
      {refusal && (
        <InlineNotice severity="error" title={texts.fileRefused}>
          {refusal}
        </InlineNotice>
      )}
      {!sample && source.headers.length > 0 && (
        <InlineNotice severity="info">{texts.sampleExpired}</InlineNotice>
      )}
      <Box
        sx={{
          display: 'grid',
          gap: 2,
          gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
          alignItems: 'start',
        }}
      >
        {source.format === 'xlsx' && read && (
          <InlineSelectField
            label={texts.sheet}
            frame="always"
            value={source.sheet ?? ''}
            readOnly={readOnly}
            options={read.sheets.map((sheet) => ({ value: sheet, label: sheet }))}
            onChange={(value) => {
              const next = { ...draft, source: { ...source, sheet: value || null } };
              readAgain(optionsOf(next), next);
            }}
          />
        )}
        <InlineNumberField
          label={texts.headerRow}
          frame="always"
          inputMode="numeric"
          value={source.headerRow === 0 ? '' : String(source.headerRow)}
          readOnly={readOnly}
          error={errors.headerRow ?? headerProblem ?? undefined}
          onChange={(text) => onDraft(numberField('headerRow', text))}
          inputProps={{ onBlur: () => readAgain(optionsOf(draft), draft) }}
        />
        <InlineNumberField
          label={texts.firstDataRow}
          frame="always"
          inputMode="numeric"
          value={source.firstDataRow === 0 ? '' : String(source.firstDataRow)}
          readOnly={readOnly}
          error={errors.firstDataRow}
          onChange={(text) => onDraft(numberField('firstDataRow', text))}
          inputProps={{ onBlur: () => readAgain(optionsOf(draft), draft) }}
        />
        {source.format === 'csv' && (
          <>
            <InlineSelectField
              label={texts.encoding}
              frame="always"
              value={source.encoding ?? ''}
              readOnly={readOnly}
              hint={read?.encodingDetected ? texts.detected : undefined}
              options={ENCODINGS}
              onChange={(value) => {
                const next = { ...draft, source: { ...source, encoding: value || null } };
                readAgain(optionsOf(next), next);
              }}
            />
            <InlineSelectField
              label={texts.delimiter}
              frame="always"
              value={source.delimiter ?? ''}
              readOnly={readOnly}
              hint={read?.delimiterDetected ? texts.detected : undefined}
              options={DELIMITERS}
              onChange={(value) => {
                const next = { ...draft, source: { ...source, delimiter: value || null } };
                readAgain(optionsOf(next), next);
              }}
            />
          </>
        )}
      </Box>
      {read && read.columns.length > 0 && (
        <TableCard
          aria-label={texts.previewLabel}
          maxHeightOffset={320}
          head={read.columns.map((column) => (
            <HeadCell
              key={column.index}
              label={texts.headerAddress(column.header, column.address)}
            />
          ))}
        >
          {read.sampleRows.map((row) => (
            <BodyRow key={row.row}>
              {read.columns.map((column) => (
                <Cell key={column.index} clip>
                  {row.cells[column.index]?.text ?? ''}
                </Cell>
              ))}
            </BodyRow>
          ))}
        </TableCard>
      )}
    </Stack>
  );
}
