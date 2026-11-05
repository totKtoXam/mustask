import type { Ref } from 'react';
import type { FieldInputProps, FieldProps } from './fieldTypes';
import { InputField } from './InputField';

export interface InlineNumberFieldProps extends FieldProps {
  /** The *text*, so a half-typed number survives; the host parses it. */
  value: string;
  onChange: (text: string) => void;
  /** The keyboard the device offers: `decimal` with a separator, `numeric` for whole numbers. */
  inputMode: 'decimal' | 'numeric';
  inputProps?: FieldInputProps;
  /** Focuses the input. */
  ref?: Ref<HTMLInputElement>;
}

/**
 * A number as typed (tech-015 design § 5.7): a text input with a numeric keyboard,
 * never `type="number"` — a half-typed «3,» must survive, and the host shows «,» and
 * no trailing zeros. The host checks it on save and says what is wrong with `error`.
 */
export function InlineNumberField({ inputMode, inputProps, ...props }: InlineNumberFieldProps) {
  return <InputField {...props} inputProps={{ ...inputProps, inputMode }} />;
}
