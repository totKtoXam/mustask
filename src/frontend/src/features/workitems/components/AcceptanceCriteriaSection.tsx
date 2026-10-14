import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import {
  ActionButton,
  IconAction,
  InlineEmpty,
  InlineTextArea,
  Section,
  Surface,
  ThinAddIcon,
  ThinRemoveIcon,
} from '../../../shared/ui';
import { createCriterionDraft, type DraftCriterion } from '../lib/draft';

export interface AcceptanceCriteriaSectionProps {
  criteria?: DraftCriterion[];
  disabled?: boolean;
  /** The card is read-only: values stay, nothing is added, removed or typed (BR-C-19). */
  readOnly?: boolean;
  errors?: Partial<Record<string, string>>;
  onChange?: (criteria: DraftCriterion[]) => void;
  onAdd?: () => void;
  onRemove?: (id: string) => void;
  onUpdate?: (id: string, field: 'given' | 'when' | 'then', value: string) => void;
  /** When true, wraps in <Section title="Критерии приёмки">. Default false. */
  standalone?: boolean;
}

export function AcceptanceCriteriaSection({
  criteria = [],
  disabled = false,
  readOnly = false,
  errors = {},
  onChange,
  onAdd,
  onRemove,
  onUpdate,
  standalone = false,
}: AcceptanceCriteriaSectionProps) {
  function handleAdd() {
    if (disabled || readOnly) return;
    const newCriterion = createCriterionDraft();
    if (onChange) {
      onChange([...criteria, newCriterion]);
    }
    onAdd?.();
  }

  function handleRemove(id: string) {
    if (disabled || readOnly) return;
    if (onChange) {
      onChange(criteria.filter((item) => item.id !== id));
    }
    onRemove?.(id);
  }

  function handleUpdate(id: string, field: 'given' | 'when' | 'then', value: string) {
    if (disabled || readOnly) return;
    if (onChange) {
      onChange(criteria.map((item) => (item.id === id ? { ...item, [field]: value } : item)));
    }
    onUpdate?.(id, field, value);
  }

  const content = (
    <Stack spacing={2} useFlexGap>
      {criteria.length === 0 ? (
        <InlineEmpty>Критерии приёмки не заданы.</InlineEmpty>
      ) : (
        criteria.map((criterion, index) => {
          const givenError =
            errors[`acceptanceCriteria[${index}].given`] ?? errors[`${criterion.id}.given`];
          const whenError =
            errors[`acceptanceCriteria[${index}].when`] ?? errors[`${criterion.id}.when`];
          const thenError =
            errors[`acceptanceCriteria[${index}].then`] ?? errors[`${criterion.id}.then`];

          return (
            <Surface key={criterion.id} variant="card" padding="dense">
              <Box
                sx={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  mb: 1.5,
                }}
              >
                <Box sx={{ typography: 'body2', fontWeight: 600 }}>Сценарий {index + 1}</Box>
                <IconAction
                  label="Удалить критерий"
                  icon={<ThinRemoveIcon />}
                  size="row"
                  disabled={disabled || readOnly}
                  onClick={() => handleRemove(criterion.id)}
                />
              </Box>
              <Stack spacing={2} useFlexGap>
                <InlineTextArea
                  label="GIVEN (предусловие)"
                  placeholder="Предусловие или контекст"
                  minRows={2}
                  value={criterion.given}
                  disabled={disabled}
                  readOnly={readOnly}
                  error={givenError}
                  onChange={(val) => handleUpdate(criterion.id, 'given', val)}
                />
                <InlineTextArea
                  label="WHEN (действие)"
                  placeholder="Действие или условие"
                  minRows={2}
                  value={criterion.when}
                  disabled={disabled}
                  readOnly={readOnly}
                  error={whenError}
                  onChange={(val) => handleUpdate(criterion.id, 'when', val)}
                />
                <InlineTextArea
                  label="THEN (ожидаемый результат)"
                  placeholder="Ожидаемый результат"
                  minRows={2}
                  value={criterion.then}
                  disabled={disabled}
                  readOnly={readOnly}
                  error={thenError}
                  onChange={(val) => handleUpdate(criterion.id, 'then', val)}
                />
              </Stack>
            </Surface>
          );
        })
      )}
      <Box>
        <ActionButton
          label="Добавить критерий"
          icon={<ThinAddIcon />}
          disabled={disabled || readOnly}
          onClick={handleAdd}
        />
      </Box>
    </Stack>
  );

  if (standalone) {
    return <Section title="Критерии приёмки">{content}</Section>;
  }

  return content;
}
