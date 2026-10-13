import type { KeyboardEvent, Ref } from 'react';
import InputBase from '@mui/material/InputBase';
import { useTheme, type CSSObject } from '@mui/material/styles';
import { FieldFrame } from './FieldFrame';
import type { FieldInputProps, FieldProps } from './fieldTypes';
import { fieldControlState, useFieldIds } from './useFieldWiring';

export interface InputFieldProps extends FieldProps {
  value: string;
  /** The value, not the event. */
  onChange?: (value: string) => void;
  /**
   * Called with the input itself on every change and every key-up — the date field
   * reads `validity.badInput` from it, because a date typed in part has no value
   * and the browser may announce no change for it.
   */
  onInputEvent?: (input: HTMLInputElement) => void;
  type?: 'text' | 'date';
  multiline?: boolean;
  minRows?: number;
  maxLength?: number;
  min?: string;
  max?: string;
  inputProps?: FieldInputProps;
  kindRules?: CSSObject;
  /** Focuses the input. */
  ref?: Ref<HTMLInputElement | HTMLTextAreaElement>;
}

/**
 * The shared body of the four fields that are an `InputBase` — text, text area,
 * number and date (tech-015 design § 5.7): the frame, and the control wired to it.
 * Internal: the kit exports the four fields, not this.
 */
export function InputField({
  value,
  onChange,
  onInputEvent,
  type = 'text',
  multiline = false,
  minRows,
  maxLength,
  min,
  max,
  inputProps,
  kindRules,
  ref,
  ...field
}: InputFieldProps) {
  const theme = useTheme();
  const ids = useFieldIds(field.id);
  const state = fieldControlState(field, ids);
  const empty = value === '';
  return (
    <FieldFrame
      {...field}
      ids={ids}
      empty={empty}
      hasPlaceholder={Boolean(field.placeholder)}
      multiline={multiline}
      kindRules={kindRules}
    >
      <InputBase
        id={ids.control}
        type={type}
        value={value}
        placeholder={field.placeholder}
        multiline={multiline}
        minRows={multiline ? (minRows ?? theme.layout.field.textMinRows) : undefined}
        readOnly={state.locked}
        disabled={field.disabled}
        inputRef={ref}
        fullWidth
        onChange={(event) => {
          onChange?.(event.target.value);
          onInputEvent?.(event.target as HTMLInputElement);
        }}
        slotProps={{
          input: {
            ...inputProps,
            ...state.aria,
            'aria-readonly': state.locked ? true : undefined,
            maxLength,
            min,
            max,
            // A date reads blank at rest: the kit says when its mask may hide (design § 5.7).
            ...(type === 'date' ? { 'data-empty': empty && !field.error ? 'true' : 'false' } : {}),
            onKeyUp: (event: KeyboardEvent<HTMLInputElement>) => {
              inputProps?.onKeyUp?.(event);
              onInputEvent?.(event.currentTarget);
            },
          },
        }}
      />
    </FieldFrame>
  );
}
