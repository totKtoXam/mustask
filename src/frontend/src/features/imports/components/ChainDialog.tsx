import { useEffect, useMemo, useRef, useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineOutlined';
import Box from '@mui/material/Box';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemText from '@mui/material/ListItemText';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import {
  ActionButton,
  BodyRow,
  Cell,
  DialogShell,
  HeadCell,
  IconAction,
  InlineNotice,
  InlineNumberField,
  InlineSelectField,
  InlineTextField,
  MenuButton,
  TableCard,
  type MenuEntry,
} from '../../../shared/ui';
import type {
  Catalogue,
  CatalogueConverter,
  ChainSample,
  ChainStep,
  ConverterId,
} from '../api/importsApi';
import { useReadChainSample } from '../hooks/mutations';
import { newStep, type DraftMapping } from '../lib/draft';
import { catalogText } from '../lib/catalogText';
import { CONVERTER_CLASS_TITLES, DATE_SYSTEMS, texts } from '../lib/texts';
import type { SampleFile } from './SourceTab';

interface LookupEntry {
  from: string;
  to: string;
}

function lookupEntries(step: ChainStep): LookupEntry[] {
  return Array.isArray(step.params.entries) ? (step.params.entries as LookupEntry[]) : [];
}

/** The validation of one step's parameters (design § 3.2): the text, or null when the step is fine. */
export function stepError(step: ChainStep, limits: Catalogue['limits']): string | null {
  const { params } = step;
  const text = (name: string) => String(params[name] ?? '').trim();
  switch (step.converter) {
    case 'truncate': {
      const length = Number(params.length);
      return Number.isInteger(length) && length >= 1 ? null : texts.fillValue;
    }
    case 'dateFormat':
      return text('format') === '' ? texts.fillValue : null;
    case 'split':
      return String(params.delimiter ?? '') === '' ? texts.fillValue : null;
    case 'extract':
      if (text('pattern') === '') return texts.fillValue;
      return text('pattern').length > limits.maxPatternLength ? texts.patternTooLong : null;
    case 'lookup':
      return lookupEntries(step).length > limits.maxRecodingEntries ? texts.lookupTooMany : null;
    default:
      return null;
  }
}

function paramsSummary(step: ChainStep, catalogue: Catalogue): string {
  const { params } = step;
  switch (step.converter) {
    case 'truncate':
      return String(params.length ?? '');
    case 'dateFormat':
      return catalogue.dateFormats.find((format) => format.id === params.format)?.name ?? '';
    case 'split':
      return `«${String(params.delimiter ?? '')}»`;
    case 'extract':
      return String(params.pattern ?? '');
    case 'excelDate':
      return String(params.system ?? '');
    case 'lookup':
      return String(lookupEntries(step).length);
    default:
      return '';
  }
}

function StepForm({
  step,
  catalogue,
  error,
  onChange,
}: {
  step: ChainStep;
  catalogue: Catalogue;
  error: string | null;
  onChange: (step: ChainStep) => void;
}) {
  const set = (patch: Record<string, unknown>) =>
    onChange({ ...step, params: { ...step.params, ...patch } });
  const { params } = step;
  switch (step.converter) {
    case 'trim':
      return (
        <FormControlLabel
          label={texts.collapseSpaces}
          control={
            <Checkbox
              checked={params.collapse === true}
              onChange={(event) => set({ collapse: event.target.checked })}
            />
          }
        />
      );
    case 'truncate':
      return (
        <InlineNumberField
          label={texts.length}
          frame="always"
          inputMode="numeric"
          value={params.length === undefined ? '' : String(params.length)}
          error={error ?? undefined}
          onChange={(value) => set({ length: value === '' ? '' : Number.parseInt(value, 10) || 0 })}
        />
      );
    case 'excelDate':
      return (
        <InlineSelectField
          label={texts.dateSystem}
          frame="always"
          value={String(params.system ?? '1900')}
          options={DATE_SYSTEMS.map((system) => ({ value: system.id, label: system.name }))}
          onChange={(value) => set({ system: value })}
        />
      );
    case 'dateFormat':
      return (
        <InlineSelectField
          label={texts.dateFormat}
          frame="always"
          value={String(params.format ?? '')}
          error={error ?? undefined}
          options={catalogue.dateFormats.map((format) => ({
            value: format.id,
            label: format.name,
          }))}
          onChange={(value) => set({ format: value })}
        />
      );
    case 'split':
      return (
        <InlineTextField
          label={texts.splitDelimiter}
          frame="always"
          value={String(params.delimiter ?? '')}
          error={error ?? undefined}
          onChange={(value) => set({ delimiter: value })}
        />
      );
    case 'extract':
      return (
        <InlineTextField
          label={texts.extractPattern}
          frame="always"
          hint={texts.extractCaption}
          value={String(params.pattern ?? '')}
          error={error ?? undefined}
          onChange={(value) => set({ pattern: value })}
        />
      );
    case 'lookup':
      return <LookupForm step={step} error={error} onChange={onChange} />;
    default:
      return null;
  }
}

function LookupForm({
  step,
  error,
  onChange,
}: {
  step: ChainStep;
  error: string | null;
  onChange: (step: ChainStep) => void;
}) {
  const entries = lookupEntries(step);
  const policy = String(step.params.policy ?? 'error');
  const set = (patch: Record<string, unknown>) =>
    onChange({ ...step, params: { ...step.params, ...patch } });
  const setEntry = (index: number, patch: Partial<LookupEntry>) =>
    set({ entries: entries.map((entry, at) => (at === index ? { ...entry, ...patch } : entry)) });
  return (
    <Stack spacing={1}>
      {error && <InlineNotice severity="error">{error}</InlineNotice>}
      <TableCard
        aria-label={texts.lookupFrom}
        maxRows={6}
        head={[
          <HeadCell key="from" label={texts.lookupFrom} />,
          <HeadCell key="to" label={texts.lookupTo} />,
          <HeadCell key="x" label="" width={40} />,
        ]}
      >
        {entries.map((entry, index) => (
          <BodyRow key={index}>
            <Cell>
              <InlineTextField
                label={`${texts.lookupFrom} ${index + 1}`}
                labelPlacement="hidden"
                frame="always"
                size="table"
                value={entry.from}
                onChange={(value) => setEntry(index, { from: value })}
              />
            </Cell>
            <Cell>
              <InlineTextField
                label={`${texts.lookupTo} ${index + 1}`}
                labelPlacement="hidden"
                frame="always"
                size="table"
                value={entry.to}
                onChange={(value) => setEntry(index, { to: value })}
              />
            </Cell>
            <Cell>
              <IconAction
                size="row"
                label={`${texts.removeStep} ${index + 1}`}
                icon={<DeleteOutlineIcon />}
                onClick={() => set({ entries: entries.filter((_, at) => at !== index) })}
              />
            </Cell>
          </BodyRow>
        ))}
      </TableCard>
      <ActionButton
        label={texts.lookupAdd}
        tone="subtle"
        icon={<AddIcon />}
        onClick={() => set({ entries: [...entries, { from: '', to: '' }] })}
      />
      <InlineSelectField
        label={texts.lookupPolicy}
        frame="always"
        value={policy}
        options={[
          { value: 'error', label: texts.lookupPolicyError },
          { value: 'default', label: texts.lookupPolicyDefault },
          { value: 'keep', label: texts.lookupPolicyKeep },
        ]}
        onChange={(value) => set({ policy: value })}
      />
      {policy === 'default' && (
        <InlineTextField
          label={texts.lookupDefault}
          frame="always"
          value={String(step.params.defaultValue ?? '')}
          onChange={(value) => set({ defaultValue: value })}
        />
      )}
    </Stack>
  );
}

function SampleTable({ sample, steps }: { sample: ChainSample; steps: ChainStep[] }) {
  return (
    <TableCard
      aria-label={texts.sample}
      maxRows={6}
      head={[
        <HeadCell key="o" label={texts.sampleOriginal} />,
        ...steps.map((_, index) => <HeadCell key={index} label={texts.sampleStep(index + 1)} />),
        <HeadCell key="r" label={texts.sampleResult} />,
      ]}
    >
      {sample.rows.map((row) => (
        <BodyRow key={row.row}>
          <Cell wrap>{row.original}</Cell>
          {steps.map((_, index) => {
            const step = row.steps[index];
            return (
              <Cell key={index} wrap>
                {step?.error ?? step?.value ?? ''}
              </Cell>
            );
          })}
          <Cell wrap>
            {row.error ? catalogText(row.error.code, row.error.params) : (row.result ?? '')}
          </Cell>
        </BodyRow>
      ))}
    </TableCard>
  );
}

/** S2a — the chain of converters of one mapping (design § 3.2). «Применить» changes the draft only. */
export function ChainDialog({
  organizationId,
  mapping,
  fieldName,
  catalogue,
  sample,
  readOnly,
  onApply,
  onClose,
}: {
  organizationId: string;
  mapping: DraftMapping;
  fieldName: string;
  catalogue: Catalogue;
  sample: SampleFile | null;
  readOnly: boolean;
  onApply: (chain: ChainStep[]) => void;
  onClose: () => void;
}) {
  const [steps, setSteps] = useState<ChainStep[]>(mapping.chain);
  const [selected, setSelected] = useState(0);
  const [showErrors, setShowErrors] = useState(false);
  const probe = useReadChainSample(organizationId);
  const probeRef = useRef(probe.mutate);
  useEffect(() => {
    probeRef.current = probe.mutate;
  });
  const { limits } = catalogue;
  const tooLong = steps.length >= limits.maxChainLength;
  const errors = steps.map((step) => stepError(step, limits));
  const current = steps[selected];
  const key = useMemo(() => JSON.stringify(steps), [steps]);

  useEffect(() => {
    if (!sample) return;
    const timer = setTimeout(() => {
      probeRef.current({
        fileId: sample.fileId,
        probe: {
          source: {
            sheet: sample.read.sheet,
            headerRow: sample.read.headerRow,
            firstDataRow: sample.read.firstDataRow,
            encoding: sample.read.encoding,
            delimiter: sample.read.delimiter,
          },
          mapping: { ...mapping, field: mapping.field, chain: JSON.parse(key) as ChainStep[] },
        },
      });
    }, 300);
    return () => clearTimeout(timer);
  }, [key, sample, mapping]);

  const groups = new Map<string, CatalogueConverter[]>();
  for (const converter of catalogue.converters.filter((candidate) => !candidate.isSource)) {
    groups.set(converter.class, [...(groups.get(converter.class) ?? []), converter]);
  }
  const menu: MenuEntry[] = [...groups.entries()].map(([group, converters]) => ({
    group: CONVERTER_CLASS_TITLES[group] ?? group,
    items: converters.map((converter) => ({
      label: converter.name,
      onSelect: () => {
        setSteps((list) => [...list, newStep(converter.id as ConverterId)]);
        setSelected(steps.length);
      },
    })),
  }));

  function move(index: number, delta: number) {
    const target = index + delta;
    if (target < 0 || target >= steps.length) return;
    const next = [...steps];
    [next[index], next[target]] = [next[target] as ChainStep, next[index] as ChainStep];
    setSteps(next);
    setSelected(target);
  }

  function apply() {
    if (errors.some((error) => error !== null)) {
      setShowErrors(true);
      const first = errors.findIndex((error) => error !== null);
      setSelected(first);
      return;
    }
    onApply(steps);
  }

  const nameOf = (step: ChainStep) =>
    catalogue.converters.find((converter) => converter.id === step.converter)?.name ??
    step.converter;

  return (
    <DialogShell
      open
      size="wide"
      title={texts.chainTitle(fieldName)}
      onClose={onClose}
      footer={
        <>
          <ActionButton label={texts.cancel} onClick={onClose} />
          {!readOnly && <ActionButton label={texts.apply} tone="primary" onClick={apply} />}
        </>
      }
    >
      <Box
        sx={{
          display: 'grid',
          gap: 3,
          gridTemplateColumns: { xs: '1fr', lg: '1fr 1fr' },
          alignItems: 'start',
        }}
      >
        <Stack spacing={1}>
          {steps.length === 0 ? (
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {texts.chainEmpty}
            </Typography>
          ) : (
            <List dense aria-label={texts.chainTitle(fieldName)}>
              {steps.map((step, index) => (
                <Box key={index} sx={{ display: 'flex', alignItems: 'center' }}>
                  <ListItemButton selected={index === selected} onClick={() => setSelected(index)}>
                    <ListItemText
                      primary={`${index + 1}. ${nameOf(step)}`}
                      secondary={paramsSummary(step, catalogue) || undefined}
                    />
                  </ListItemButton>
                  {!readOnly && (
                    <>
                      <IconAction
                        size="row"
                        label={`${texts.moveUp}: ${nameOf(step)} ${index + 1}`}
                        icon={<ArrowUpwardIcon />}
                        disabled={index === 0}
                        onClick={() => move(index, -1)}
                      />
                      <IconAction
                        size="row"
                        label={`${texts.moveDown}: ${nameOf(step)} ${index + 1}`}
                        icon={<ArrowDownwardIcon />}
                        disabled={index === steps.length - 1}
                        onClick={() => move(index, 1)}
                      />
                      <IconAction
                        size="row"
                        label={`${texts.removeStep}: ${nameOf(step)} ${index + 1}`}
                        icon={<DeleteOutlineIcon />}
                        onClick={() => {
                          setSteps(steps.filter((_, at) => at !== index));
                          setSelected(Math.max(0, index - 1));
                        }}
                      />
                    </>
                  )}
                </Box>
              ))}
            </List>
          )}
          {!readOnly && (
            <>
              <MenuButton
                label={texts.addConverter}
                items={menu}
                trigger={(props) => (
                  <ActionButton
                    label={texts.addConverter}
                    tone="subtle"
                    icon={<AddIcon />}
                    disabled={tooLong}
                    {...props}
                  />
                )}
              />
              {tooLong && (
                <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                  {texts.chainTooLong}
                </Typography>
              )}
            </>
          )}
        </Stack>
        <Stack spacing={2}>
          {current && (
            <Stack spacing={1} aria-label={texts.stepParams(nameOf(current))} role="group">
              <StepForm
                step={current}
                catalogue={catalogue}
                error={showErrors ? (errors[selected] ?? null) : null}
                onChange={(next) =>
                  readOnly
                    ? undefined
                    : setSteps(steps.map((step, at) => (at === selected ? next : step)))
                }
              />
            </Stack>
          )}
          <Typography variant="subtitle2" component="p">
            {texts.sample}
          </Typography>
          {!sample ? (
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {texts.noSample}
            </Typography>
          ) : probe.isError ? (
            <InlineNotice severity="error" title={texts.sampleFailed}>
              {apiErrorText(probe.error)}
            </InlineNotice>
          ) : probe.data ? (
            <SampleTable sample={probe.data} steps={steps} />
          ) : null}
        </Stack>
      </Box>
    </DialogShell>
  );
}
