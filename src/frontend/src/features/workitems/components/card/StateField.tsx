import { memo } from 'react';
import type { Ref } from 'react';
import { CategoryMarker, InlineSelectField } from '../../../../shared/ui';
import type { WorkItemStateReference } from '../../api/workItemsApi';
import { cardTexts } from '../../lib/cardTexts';

/**
 * «Статус» of the band (US-5; mod-003 spec § Pickers): the type's states in the
 * seeded order without «Удалена», each with its category dot in the list. The
 * chosen state's dot stands in the label column (the kit's default for a field
 * with its label at the left) and the value is the state's name alone, so the
 * text starts right after the dot. Any state is reachable from any other
 * (BR-M3-21); the choice is saved with «Сохранить», like every field.
 */
function StateFieldView({
  states,
  value,
  error,
  busy,
  readOnly,
  onChange,
  inputRef,
}: {
  states: readonly WorkItemStateReference[];
  /** The state's id. */
  value: string;
  error: string | undefined;
  busy: boolean;
  readOnly: boolean;
  onChange: (stateId: string) => void;
  inputRef?: Ref<HTMLElement>;
}) {
  const current = states.find((state) => state.id === value);
  return (
    <InlineSelectField<string>
      label={cardTexts.state.label}
      labelPlacement="start"
      value={value}
      options={states.map((state) => ({
        value: state.id,
        label: state.name,
        startAdornment: <CategoryMarker code={state.category.code} size="field" />,
      }))}
      onChange={(next) => {
        if (next !== '') {
          onChange(next);
        }
      }}
      startAdornment={
        current ? <CategoryMarker code={current.category.code} size="field" /> : undefined
      }
      renderValue={(option) => option.label}
      error={error}
      busy={busy}
      readOnly={readOnly}
      ref={inputRef}
    />
  );
}

export const StateField = memo(StateFieldView);
