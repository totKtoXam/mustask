import type { Ref } from 'react';
import type { FieldInputProps, FieldProps } from './fieldTypes';
import { InputField } from './InputField';

export interface InlineTextAreaProps extends Omit<FieldProps, 'size'> {
  value: string;
  onChange: (value: string) => void;
  /** The least rows; default `layout.field.textMinRows`. The area grows with its text. */
  minRows?: number;
  inputProps?: FieldInputProps;
  /** Focuses the text area. */
  ref?: Ref<HTMLTextAreaElement>;
}

/**
 * A multi-line text (tech-015 design § 5.7), the body size only. Its text starts at
 * `layout.field.textAreaInset`, a little further in than a single line's.
 *
 * With `labelPlacement="hidden"` it is **a text under a head of its own name** — the
 * card's «Описание», «Критерии приёмки»: the `label` is the head's own text, so no
 * second small label is drawn, and the box begins `layout.field.textAreaTop` below
 * where the section's body starts, which puts the text where the reference sets it.
 */
export function InlineTextArea(props: InlineTextAreaProps) {
  // The body size, whatever a caller that bypasses the types says.
  return <InputField {...props} size="body" multiline />;
}
