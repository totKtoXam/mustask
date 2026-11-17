import { useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlineOutlined';
import TuneIcon from '@mui/icons-material/Tune';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import {
  ActionButton,
  BodyRow,
  Cell,
  HeadCell,
  IconAction,
  InlineNotice,
  InlineSelectField,
  InlineTextField,
  Tag,
  TableCard,
} from '../../../shared/ui';
import type { Catalogue, CatalogueField, ChainStep, MappingSource } from '../api/importsApi';
import { useReferences } from '../hooks/useReferences';
import {
  blankMapping,
  emptyRecoding,
  missingColumns,
  recodingOf,
  removeMapping,
  updateMapping,
  upsertRecoding,
  type Draft,
  type DraftErrors,
  type DraftMapping,
} from '../lib/draft';
import { texts } from '../lib/texts';
import { ChainDialog } from './ChainDialog';
import type { SampleFile } from './SourceTab';

const COLUMN_PREFIX = 'col:';

function sourceChoice(source: MappingSource): string {
  if (source.kind === 'constant') return 'constant';
  if (source.kind === 'merge') return 'merge';
  return source.column ? `${COLUMN_PREFIX}${source.column}` : '';
}

/**
 * Tab «Сопоставление» (design § 3.2): one row per mapping — a source feeding a field. A column the
 * template leaves out is a row with «Не переносить». Rights are not asked here: the editor is either
 * writable or read-only, as the server said.
 */
export function MappingTab({
  organizationId,
  draft,
  onDraft,
  catalogue,
  sample,
  errors,
  readOnly,
  onJumpToValues,
}: {
  organizationId: string;
  draft: Draft;
  onDraft: (draft: Draft) => void;
  catalogue: Catalogue;
  sample: SampleFile | null;
  errors: DraftErrors;
  readOnly: boolean;
  onJumpToValues: () => void;
}) {
  const references = useReferences(organizationId, true);
  const [chainOf, setChainOf] = useState<string | null>(null);
  const fieldOf = (id: string): CatalogueField | undefined =>
    catalogue.fields.find((field) => field.id === id);
  const { headers } = draft.source;

  function setField(mapping: DraftMapping, fieldId: string) {
    let next = updateMapping(draft, mapping.id, {
      field: fieldId,
      emptyCell: 'keep',
      fieldAbsent: 'error',
    });
    const field = fieldOf(fieldId);
    if (field && field.recoding !== 'none') {
      if (!recodingOf(next, mapping.id)) next = upsertRecoding(next, emptyRecoding(mapping.id));
    } else {
      next = { ...next, recoding: next.recoding.filter((entry) => entry.mappingId !== mapping.id) };
    }
    onDraft(next);
  }

  function setSource(mapping: DraftMapping, choice: string) {
    if (choice === 'constant') {
      onDraft(
        updateMapping(draft, mapping.id, { source: { kind: 'constant', value: '' }, chain: [] }),
      );
    } else if (choice === 'merge') {
      onDraft(
        updateMapping(draft, mapping.id, { source: { kind: 'merge', pattern: '' }, chain: [] }),
      );
    } else {
      onDraft(
        updateMapping(draft, mapping.id, {
          source: { kind: 'column', column: choice.slice(COLUMN_PREFIX.length) },
        }),
      );
    }
  }

  function fieldOptions(mapping: DraftMapping) {
    const taken = new Set(
      draft.mappings.filter((other) => other.id !== mapping.id).map((other) => other.field),
    );
    return catalogue.fields
      .filter((field) => !taken.has(field.id))
      .map((field) => ({ value: field.id, label: field.name }));
  }

  const chainMapping = draft.mappings.find((mapping) => mapping.id === chainOf);
  const missingFields = errors.missingRequired
    .map((id) => fieldOf(id)?.name)
    .filter((name): name is string => name !== undefined);

  function renderSource(mapping: DraftMapping) {
    const absent = missingColumns(mapping.source, headers).length > 0 && headers.length > 0;
    const field = fieldOf(mapping.field);
    return (
      <Stack spacing={0.5}>
        <InlineSelectField
          label={`${texts.sourceLabel}: ${mapping.field || texts.notTransferred}`}
          labelPlacement="hidden"
          size="table"
          frame="always"
          value={sourceChoice(mapping.source)}
          readOnly={readOnly || mapping.field === ''}
          error={
            errors.mappings[mapping.id] && mapping.source.kind === 'column'
              ? errors.mappings[mapping.id]
              : undefined
          }
          options={[
            ...headers.map((header) => ({
              value: `${COLUMN_PREFIX}${header}`,
              label: texts.sourceColumn(header),
            })),
            ...(mapping.source.kind === 'column' &&
            mapping.source.column &&
            !headers.includes(mapping.source.column)
              ? [
                  {
                    value: `${COLUMN_PREFIX}${mapping.source.column}`,
                    label: texts.sourceColumn(mapping.source.column),
                  },
                ]
              : []),
            ...(mapping.field === ''
              ? []
              : [
                  { value: 'constant', label: texts.constant },
                  { value: 'merge', label: texts.merge },
                ]),
          ]}
          onChange={(choice) => setSource(mapping, choice)}
        />
        {absent && mapping.source.kind === 'column' && (
          <Tag tone="warning" label={texts.absentFromFile} />
        )}
        {mapping.source.kind === 'constant' &&
          (field && field.recoding !== 'none' ? (
            <InlineSelectField
              label={`${texts.constantValue}: ${field.name}`}
              labelPlacement="hidden"
              size="table"
              frame="always"
              value={mapping.source.value ?? ''}
              readOnly={readOnly}
              error={errors.mappings[mapping.id]}
              options={references.options(field.recoding).map((option) => ({
                value: option.value,
                label: option.label,
              }))}
              onChange={(value) =>
                onDraft(updateMapping(draft, mapping.id, { source: { kind: 'constant', value } }))
              }
            />
          ) : (
            <InlineTextField
              label={`${texts.constantValue}: ${field?.name ?? ''}`}
              labelPlacement="hidden"
              size="table"
              frame="always"
              value={mapping.source.value ?? ''}
              readOnly={readOnly}
              error={errors.mappings[mapping.id]}
              onChange={(value) =>
                onDraft(updateMapping(draft, mapping.id, { source: { kind: 'constant', value } }))
              }
            />
          ))}
        {mapping.source.kind === 'merge' && (
          <InlineTextField
            label={`${texts.mergePattern}: ${field?.name ?? ''}`}
            labelPlacement="hidden"
            size="table"
            frame="always"
            hint={texts.mergeHint}
            placeholder={texts.mergePattern}
            value={mapping.source.pattern ?? ''}
            readOnly={readOnly}
            error={errors.mappings[mapping.id]}
            onChange={(pattern) =>
              onDraft(updateMapping(draft, mapping.id, { source: { kind: 'merge', pattern } }))
            }
          />
        )}
      </Stack>
    );
  }

  function renderChain(mapping: DraftMapping) {
    if (mapping.field === '' || mapping.source.kind !== 'column') return texts.noConverters;
    const field = fieldOf(mapping.field);
    const names = mapping.chain.map(
      (step: ChainStep) =>
        catalogue.converters.find((converter) => converter.id === step.converter)?.name ??
        step.converter,
    );
    const recoded = recodingOf(draft, mapping.id);
    return (
      <Stack
        direction="row"
        spacing={0.5}
        useFlexGap
        sx={{ flexWrap: 'wrap', alignItems: 'center' }}
      >
        {names.map((name, index) => (
          <Tag key={index} label={name} variant="outlined" />
        ))}
        {(!readOnly || mapping.chain.length > 0) && (
          <ActionButton
            label={mapping.chain.length > 0 ? texts.editChain : texts.addChain}
            aria-label={`${texts.mappingColumns.chain}: ${field?.name ?? ''}`}
            tone="subtle"
            size="compact"
            icon={<TuneIcon />}
            onClick={() => setChainOf(mapping.id)}
          />
        )}
        {recoded && (
          <ActionButton
            label={texts.valuesChip(recoded.entries.length)}
            tone="subtle"
            size="compact"
            onClick={onJumpToValues}
          />
        )}
      </Stack>
    );
  }

  return (
    <Stack spacing={2}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 2 }}>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {texts.mappingFooter}
        </Typography>
        {!readOnly && (
          <ActionButton
            label={texts.addMapping}
            tone="subtle"
            icon={<AddIcon />}
            onClick={() => onDraft({ ...draft, mappings: [...draft.mappings, blankMapping()] })}
          />
        )}
      </Box>
      {missingFields.map((name) => (
        <InlineNotice key={name} severity="error">
          {texts.missingFieldLine(name)}
        </InlineNotice>
      ))}
      <TableCard
        aria-label={texts.mappingTable}
        minWidth={880}
        head={[
          <HeadCell key="source" label={texts.mappingColumns.source} />,
          <HeadCell key="field" label={texts.mappingColumns.field} />,
          <HeadCell key="chain" label={texts.mappingColumns.chain} />,
          <HeadCell key="empty" label={texts.mappingColumns.empty} />,
          <HeadCell key="absent" label={texts.mappingColumns.absent} />,
          <HeadCell key="actions" label="" width={48} />,
        ]}
      >
        {draft.mappings.map((mapping) => {
          const field = fieldOf(mapping.field);
          return (
            <BodyRow key={mapping.id}>
              <Cell>{renderSource(mapping)}</Cell>
              <Cell>
                <InlineSelectField
                  label={`${texts.fieldLabel}: ${mapping.source.column ?? mapping.source.kind}`}
                  labelPlacement="hidden"
                  size="table"
                  frame="always"
                  value={mapping.field}
                  readOnly={readOnly}
                  emptyLabel={texts.notTransferred}
                  options={fieldOptions(mapping)}
                  onChange={(value) => setField(mapping, value)}
                />
              </Cell>
              <Cell>{renderChain(mapping)}</Cell>
              <Cell>
                {field ? (
                  <InlineSelectField
                    label={`${texts.mappingColumns.empty}: ${field.name}`}
                    labelPlacement="hidden"
                    size="table"
                    frame="always"
                    value={mapping.emptyCell}
                    readOnly={readOnly}
                    options={[
                      { value: 'keep', label: texts.emptyKeep },
                      ...(field.supportsClear
                        ? [{ value: 'clear' as const, label: texts.emptyClear }]
                        : []),
                    ]}
                    onChange={(value) =>
                      onDraft(
                        updateMapping(draft, mapping.id, {
                          emptyCell: value === 'clear' ? 'clear' : 'keep',
                        }),
                      )
                    }
                  />
                ) : (
                  texts.noConverters
                )}
              </Cell>
              <Cell>
                {field && field.typeKinds !== null ? (
                  <InlineSelectField
                    label={`${texts.mappingColumns.absent}: ${field.name}`}
                    labelPlacement="hidden"
                    size="table"
                    frame="always"
                    value={mapping.fieldAbsent}
                    readOnly={readOnly}
                    options={[
                      { value: 'error', label: texts.absentError },
                      { value: 'drop', label: texts.absentDrop },
                    ]}
                    onChange={(value) =>
                      onDraft(
                        updateMapping(draft, mapping.id, {
                          fieldAbsent: value === 'drop' ? 'drop' : 'error',
                        }),
                      )
                    }
                  />
                ) : (
                  texts.noConverters
                )}
              </Cell>
              <Cell>
                {!readOnly && (
                  <IconAction
                    size="row"
                    label={texts.removeMapping}
                    icon={<DeleteOutlineIcon />}
                    onClick={() => onDraft(removeMapping(draft, mapping.id))}
                  />
                )}
              </Cell>
            </BodyRow>
          );
        })}
      </TableCard>
      {chainMapping && (
        <ChainDialog
          organizationId={organizationId}
          mapping={chainMapping}
          fieldName={fieldOf(chainMapping.field)?.name ?? ''}
          catalogue={catalogue}
          sample={sample}
          readOnly={readOnly}
          onApply={(chain) => {
            onDraft(updateMapping(draft, chainMapping.id, { chain }));
            setChainOf(null);
          }}
          onClose={() => setChainOf(null)}
        />
      )}
    </Stack>
  );
}
