import FormControlLabel from '@mui/material/FormControlLabel';
import Radio from '@mui/material/Radio';
import RadioGroup from '@mui/material/RadioGroup';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { InlineNotice, InlineSelectField } from '../../../shared/ui';
import { identifierMapping, setIdentifierColumn, type Draft } from '../lib/draft';
import { texts } from '../lib/texts';

const COLUMN_PREFIX = 'col:';

/**
 * Tab «Идентификатор» (design § 3.4): the column that carries the MUSTASK identifier. Choosing one
 * creates or moves the mapping to «Идентификатор MUSTASK», which also shows on «Сопоставление»: the
 * model has one source of truth.
 */
export function IdentifierTab({
  draft,
  onDraft,
  readOnly,
}: {
  draft: Draft;
  onDraft: (draft: Draft) => void;
  readOnly: boolean;
}) {
  const mapping = identifierMapping(draft);
  const column = mapping?.source.kind === 'column' ? (mapping.source.column ?? '') : '';
  return (
    <Stack spacing={2} sx={{ maxWidth: 640 }}>
      <InlineSelectField
        label={texts.identifierColumn}
        frame="always"
        value={column ? `${COLUMN_PREFIX}${column}` : ''}
        readOnly={readOnly}
        emptyLabel={texts.identifierNone}
        options={draft.source.headers.map((header) => ({
          value: `${COLUMN_PREFIX}${header}`,
          label: header,
        }))}
        onChange={(value) =>
          onDraft(setIdentifierColumn(draft, value ? value.slice(COLUMN_PREFIX.length) : null))
        }
      />
      <InlineNotice severity="info">{texts.identifierHint}</InlineNotice>
      <RadioGroup
        aria-label={texts.emptyIdentifierLegend}
        value={draft.emptyIdentifier}
        onChange={(event) =>
          onDraft({ ...draft, emptyIdentifier: event.target.value === 'skip' ? 'skip' : 'create' })
        }
      >
        <Typography variant="body2" component="p" sx={{ fontWeight: 600 }}>
          {texts.emptyIdentifierLegend}
        </Typography>
        <FormControlLabel
          value="create"
          control={<Radio disabled={readOnly || !mapping} />}
          label={texts.emptyIdentifierCreate}
        />
        <FormControlLabel
          value="skip"
          control={<Radio disabled={readOnly || !mapping} />}
          label={texts.emptyIdentifierSkip}
        />
      </RadioGroup>
      {!mapping && (
        <Typography variant="caption" sx={{ color: 'text.secondary' }}>
          {texts.emptyIdentifierDisabled}
        </Typography>
      )}
    </Stack>
  );
}
