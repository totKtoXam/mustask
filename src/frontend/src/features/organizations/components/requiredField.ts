import type { FocusEvent } from 'react';
import type { TextFieldProps } from '@mui/material/TextField';

/**
 * A required field of this slice's dialogs: the label's asterisk and
 * `aria-required`, but no native `required`, so the browser never blocks the
 * submit with its own message — the length and key rules are checked on
 * submit with the design's texts (design § 4.4). `aria-invalid` follows the
 * field's refusal.
 */
export function requiredField(
  invalid: boolean,
  onFocus?: (event: FocusEvent<HTMLInputElement>) => void,
): TextFieldProps['slotProps'] {
  return {
    inputLabel: { required: true },
    htmlInput: { 'aria-required': true, 'aria-invalid': invalid, onFocus },
  };
}
