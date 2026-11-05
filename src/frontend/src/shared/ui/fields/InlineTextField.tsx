import type { Ref } from 'react';
import type { FieldInputProps, FieldProps } from './fieldTypes';
import { InputField } from './InputField';

export interface InlineTextFieldProps extends FieldProps {
  value: string;
  /** The value, not the event. */
  onChange: (value: string) => void;
  maxLength?: number;
  inputProps?: FieldInputProps;
  /** Focuses the input. */
  ref?: Ref<HTMLInputElement>;
}

/**
 * A one-line text (tech-015 design § 5.7): a label, the value as text, a frame on
 * hover and focus. A value that does not fit shows an ellipsis at rest — the card's
 * long title — and scrolls as an input does while the field has focus. `size`
 * `title` is the card's id and title, `table` a table cell's text.
 */
export function InlineTextField(props: InlineTextFieldProps) {
  return <InputField {...props} />;
}
