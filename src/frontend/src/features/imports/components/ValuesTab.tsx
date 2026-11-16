import { useEffect, useMemo, useRef, useState } from 'react';
import AddCircleOutlineIcon from '@mui/icons-material/AddCircleOutlineOutlined';
import DoneAllIcon from '@mui/icons-material/DoneAll';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined';
import RefreshIcon from '@mui/icons-material/Refresh';
import Accordion from '@mui/material/Accordion';
import AccordionDetails from '@mui/material/AccordionDetails';
import AccordionSummary from '@mui/material/AccordionSummary';
import FormControlLabel from '@mui/material/FormControlLabel';
import Radio from '@mui/material/Radio';
import RadioGroup from '@mui/material/RadioGroup';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import {
  ActionButton,
  BodyRow,
  Cell,
  HeadCell,
  IconAction,
  InlineNotice,
  InlineSelectField,
  StateFrame,
  Tag,
  TableCard,
  TablePager,
} from '../../../shared/ui';
import type {
  Catalogue,
  CatalogueField,
  FileValues,
  Recoding,
  RecodingEntry,
  RecodingPolicy,
} from '../api/importsApi';
import { useReadFileValues } from '../hooks/mutations';
import { useReferences, type ReferenceOption } from '../hooks/useReferences';
import {
  emptyRecoding,
  recodingOf,
  upsertRecoding,
  type Draft,
  type DraftErrors,
  type DraftMapping,
} from '../lib/draft';
import { texts } from '../lib/texts';
import type { SampleFile } from './SourceTab';

const PAGE_SIZE = 25;
type ValueFilter = 'all' | 'without' | 'new' | 'outdated';

const same = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase();

interface ValueRow {
  value: string;
  rows: number | null;
  general: RecodingEntry | undefined;
  typed: RecodingEntry[];
  isNew: boolean;
  outdated: boolean;
  suggestion: ReferenceOption | undefined;
}

function buildRows(
  recoding: Recoding,
  file: FileValues | undefined,
  kind: CatalogueField['recoding'],
  options: (typeId?: string | null) => ReferenceOption[],
): ValueRow[] {
  const values: { value: string; rows: number | null }[] = [];
  for (const entry of file?.values ?? []) values.push({ value: entry.value, rows: entry.rows });
  for (const entry of recoding.entries) {
    if (!values.some((known) => same(known.value, entry.fileValue))) {
      values.push({ value: entry.fileValue, rows: file ? 0 : null });
    }
  }
  return values.map(({ value, rows }) => {
    const entries = recoding.entries.filter((entry) => same(entry.fileValue, value));
    const general = entries.find((entry) => entry.typeId === null);
    const typed = entries.filter((entry) => entry.typeId !== null);
    const exists = (entry: RecodingEntry) =>
      entry.target === '' || options(entry.typeId).some((option) => option.value === entry.target);
    const suggestion =
      general === undefined ? options(null).find((option) => same(option.label, value)) : undefined;
    return {
      value,
      rows,
      general,
      typed,
      isNew: entries.length === 0 && rows !== null,
      outdated: kind !== 'none' && entries.some((entry) => !exists(entry)),
      suggestion,
    };
  });
}

function MappingValues({
  organizationId,
  mapping,
  field,
  draft,
  onDraft,
  sample,
  error,
  readOnly,
}: {
  organizationId: string;
  mapping: DraftMapping;
  field: CatalogueField;
  draft: Draft;
  onDraft: (draft: Draft) => void;
  sample: SampleFile | null;
  error: string | undefined;
  readOnly: boolean;
}) {
  const references = useReferences(organizationId, true);
  const read = useReadFileValues(organizationId);
  const readRef = useRef(read.mutate);
  useEffect(() => {
    readRef.current = read.mutate;
  });
  const recoding = recodingOf(draft, mapping.id) ?? emptyRecoding(mapping.id);
  const [filter, setFilter] = useState<ValueFilter>('all');
  const [page, setPage] = useState(0);
  const sourceKey = JSON.stringify([mapping.source, mapping.chain]);
  const kind = field.recoding;
  const file = read.data;

  function load() {
    if (!sample) return;
    readRef.current({
      fileId: sample.fileId,
      probe: {
        source: {
          sheet: sample.read.sheet,
          headerRow: sample.read.headerRow,
          firstDataRow: sample.read.firstDataRow,
          encoding: sample.read.encoding,
          delimiter: sample.read.delimiter,
        },
        mapping: { ...mapping, field: mapping.field },
      },
    });
  }

  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(load, [sample?.fileId, sourceKey]);

  const optionsFor = (typeId?: string | null) => references.options(kind, typeId);
  const rows = useMemo(
    () => buildRows(recoding, file, kind, optionsFor),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [recoding, file, kind, references.loading, references.failed],
  );
  const withoutCount = rows.filter((row) => !row.general || row.general.target === '').length;
  const newCount = rows.filter((row) => row.isNew).length;
  const shown = rows.filter((row) => {
    if (filter === 'without') return !row.general || row.general.target === '';
    if (filter === 'new') return row.isNew;
    if (filter === 'outdated') return row.outdated;
    return true;
  });
  const pageRows = shown.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE);
  const column = mapping.source.column ?? '';

  const save = (next: Recoding) => onDraft(upsertRecoding(draft, next));

  function setTarget(value: string, typeId: string | null, target: string) {
    const rest = recoding.entries.filter(
      (entry) => !(same(entry.fileValue, value) && entry.typeId === typeId),
    );
    save({
      ...recoding,
      entries: [...rest, { fileValue: value, typeId, target, onExport: false }],
    });
  }

  function removeEntry(value: string, typeId: string | null) {
    save({
      ...recoding,
      entries: recoding.entries.filter(
        (entry) => !(same(entry.fileValue, value) && entry.typeId === typeId),
      ),
    });
  }

  function acceptAll() {
    const next = [...recoding.entries];
    for (const row of rows) {
      if (row.suggestion && !row.general) {
        next.push({
          fileValue: row.value,
          typeId: null,
          target: row.suggestion.value,
          onExport: false,
        });
      }
    }
    save({ ...recoding, entries: next });
  }

  if (file?.exceeded) {
    return <InlineNotice severity="warning">{texts.tooManyValues(column)}</InlineNotice>;
  }

  const columns = [
    <HeadCell key="v" label={texts.fileValue} />,
    <HeadCell key="r" label={texts.rowsCount} />,
    ...(kind === 'state' ? [<HeadCell key="t" label={texts.forType} />] : []),
    <HeadCell key="m" label={texts.mustaskValue} />,
    <HeadCell key="s" label={texts.status} />,
    <HeadCell key="a" label="" width={48} />,
  ];

  function targetField(
    row: ValueRow,
    entry: RecodingEntry | undefined,
    typeId: string | null,
    outdated: boolean,
  ) {
    return (
      <InlineSelectField
        label={`${texts.mustaskValue}: ${row.value}`}
        labelPlacement="hidden"
        size="table"
        frame="always"
        value={entry?.target ?? ''}
        readOnly={readOnly}
        error={outdated ? texts.statusOutdatedHint : undefined}
        hint={typeId ? texts.typeValueHint : undefined}
        emptyLabel=""
        options={optionsFor(typeId).map((option) => ({
          value: option.value,
          // a state with no type chosen is told from the same name of another type by its type
          label: !typeId && option.group ? `${option.group} · ${option.label}` : option.label,
        }))}
        onChange={(target) =>
          target === '' ? removeEntry(row.value, typeId) : setTarget(row.value, typeId, target)
        }
      />
    );
  }

  return (
    <Stack spacing={2}>
      {error && <InlineNotice severity="error">{error}</InlineNotice>}
      {references.failed && (
        <InlineNotice
          severity="error"
          title={texts.referencesFailed}
          action={{ label: texts.retry, onClick: references.retry }}
        >
          {texts.referencesFailed}
        </InlineNotice>
      )}
      {read.isError && (
        <InlineNotice
          severity="error"
          title={texts.valuesFailed}
          action={{ label: texts.retry, onClick: load }}
        >
          {apiErrorText(read.error)}
        </InlineNotice>
      )}
      <RadioGroup
        row
        aria-label={texts.policyLegend}
        value={recoding.policy}
        onChange={(event) => save({ ...recoding, policy: event.target.value as RecodingPolicy })}
      >
        <FormControlLabel
          value="error"
          control={<Radio disabled={readOnly} />}
          label={texts.policyError}
        />
        <FormControlLabel
          value="default"
          control={<Radio disabled={readOnly} />}
          label={texts.policyDefault}
        />
        {field.allowsEmptyPolicy && (
          <FormControlLabel
            value="empty"
            control={<Radio disabled={readOnly} />}
            label={texts.policyEmpty}
          />
        )}
      </RadioGroup>
      {recoding.policy === 'default' && (
        <InlineSelectField
          label={texts.policyDefault}
          frame="always"
          required
          value={recoding.defaultValue ?? ''}
          readOnly={readOnly}
          error={error === texts.chooseValue ? error : undefined}
          options={optionsFor(null).map((option) => ({ value: option.value, label: option.label }))}
          onChange={(value) => save({ ...recoding, defaultValue: value || null })}
        />
      )}
      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap', alignItems: 'center' }}>
        {!readOnly && (
          <ActionButton
            label={texts.acceptAll}
            tone="subtle"
            icon={<DoneAllIcon />}
            onClick={acceptAll}
          />
        )}
        <ActionButton
          label={texts.refreshValues}
          tone="subtle"
          icon={<RefreshIcon />}
          disabled={!sample}
          busy={read.isPending}
          onClick={load}
        />
        <InlineSelectField
          label={texts.filterLabel}
          frame="always"
          value={filter}
          options={[
            { value: 'all', label: texts.filterAll },
            { value: 'without', label: texts.filterWithout },
            { value: 'new', label: texts.filterNew },
            { value: 'outdated', label: texts.filterOutdated },
          ]}
          onChange={(value) => {
            setFilter((value || 'all') as ValueFilter);
            setPage(0);
          }}
        />
      </Stack>
      {!sample && (
        <Typography variant="caption" sx={{ color: 'text.secondary' }}>
          {texts.noFileCounts}
        </Typography>
      )}
      <TableCard
        aria-label={texts.valuesTable(field.name)}
        minWidth={720}
        head={columns}
        foot={
          shown.length > PAGE_SIZE ? (
            <TablePager
              page={page}
              rowsPerPage={PAGE_SIZE}
              count={shown.length}
              onPageChange={setPage}
            />
          ) : undefined
        }
      >
        {pageRows.flatMap((row) => [
          <BodyRow key={row.value}>
            <Cell wrap>{row.value}</Cell>
            <Cell>{row.rows === null ? texts.noCount : row.rows}</Cell>
            {kind === 'state' && <Cell>{texts.allTypes}</Cell>}
            <Cell>
              <Stack spacing={0.5}>
                {targetField(row, row.general, null, row.outdated && row.general !== undefined)}
                {row.suggestion && !row.general && !readOnly && (
                  <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                    <Tag variant="outlined" label={texts.suggested(row.suggestion.label)} />
                    <ActionButton
                      label={texts.accept}
                      aria-label={`${texts.accept}: ${row.value}`}
                      tone="subtle"
                      size="compact"
                      onClick={() =>
                        setTarget(row.value, null, (row.suggestion as ReferenceOption).value)
                      }
                    />
                  </Stack>
                )}
              </Stack>
            </Cell>
            <Cell>
              {row.isNew && <Tag tone="accent" label={texts.statusNew} />}
              {row.outdated && <Tag tone="warning" label={texts.statusOutdated} />}
            </Cell>
            <Cell>
              {kind === 'state' && !readOnly && (
                <IconAction
                  size="row"
                  label={`${texts.typeValue}: ${row.value}`}
                  icon={<AddCircleOutlineIcon />}
                  onClick={() => {
                    const first = references.types.find(
                      (type) => !row.typed.some((entry) => entry.typeId === type.value),
                    );
                    if (first) setTarget(row.value, first.value, '');
                  }}
                />
              )}
            </Cell>
          </BodyRow>,
          ...row.typed.map((entry) => (
            <BodyRow key={`${row.value}:${entry.typeId}`}>
              <Cell />
              <Cell />
              <Cell>
                <InlineSelectField
                  label={`${texts.forType}: ${row.value}`}
                  labelPlacement="hidden"
                  size="table"
                  frame="always"
                  value={entry.typeId ?? ''}
                  readOnly={readOnly}
                  options={references.types.map((type) => ({
                    value: type.value,
                    label: type.label,
                  }))}
                  onChange={(typeId) => {
                    removeEntry(row.value, entry.typeId);
                    setTarget(row.value, typeId, '');
                  }}
                />
              </Cell>
              <Cell>{targetField(row, entry, entry.typeId, false)}</Cell>
              <Cell />
              <Cell />
            </BodyRow>
          )),
        ])}
      </TableCard>
      <Stack direction="row" spacing={1}>
        <Tag tone="warning" label={texts.valuesWithout(withoutCount)} />
        <Tag tone="accent" label={texts.valuesNew(newCount)} />
      </Stack>
    </Stack>
  );
}

/**
 * Tab «Значения» (design § 3.3): one accordion per recoded mapping — a table of file values and the
 * MUSTASK values they become. Only what exists can be picked (BR-IE-7): no free text, no «Создать».
 */
export function ValuesTab({
  organizationId,
  draft,
  onDraft,
  catalogue,
  sample,
  errors,
  readOnly,
}: {
  organizationId: string;
  draft: Draft;
  onDraft: (draft: Draft) => void;
  catalogue: Catalogue;
  sample: SampleFile | null;
  errors: DraftErrors;
  readOnly: boolean;
}) {
  const recoded = draft.mappings.flatMap((mapping) => {
    const field = catalogue.fields.find((candidate) => candidate.id === mapping.field);
    return field && field.recoding !== 'none' && mapping.source.kind === 'column'
      ? [{ mapping, field }]
      : [];
  });

  if (recoded.length === 0) {
    return (
      <StateFrame
        compact
        icon={InboxOutlinedIcon}
        heading={texts.valuesEmptyHeading}
        hint={texts.valuesEmptyHint}
      />
    );
  }
  return (
    <Stack spacing={1}>
      {recoded.map(({ mapping, field }) => (
        <Accordion key={mapping.id} defaultExpanded disableGutters>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography component="span">
              {texts.valuesAccordion(field.name, mapping.source.column ?? '')}
            </Typography>
          </AccordionSummary>
          <AccordionDetails>
            <MappingValues
              organizationId={organizationId}
              mapping={mapping}
              field={field}
              draft={draft}
              onDraft={onDraft}
              sample={sample}
              error={errors.recoding[mapping.id]}
              readOnly={readOnly}
            />
          </AccordionDetails>
        </Accordion>
      ))}
    </Stack>
  );
}
