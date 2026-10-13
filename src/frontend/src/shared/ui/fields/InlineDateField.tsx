import type { Ref } from 'react';
import type { CSSObject } from '@mui/material/styles';
import type { FieldFrameKind, FieldInputProps, FieldProps } from './fieldTypes';
import { InputField } from './InputField';

export interface InlineDateFieldProps extends FieldProps {
  /** `YYYY-MM-DD`, or `''` for none (and for a date typed only in part). */
  value: string;
  onChange: (value: string) => void;
  min?: string;
  max?: string;
  /**
   * Called on a change or a key-up while the entry is partial: a date typed only in
   * part has no value but a bad input, and the browser may announce no change for it
   * (feat-022) — so the host can say «Введите дату полностью.» on save. A partial entry
   * first reports `onChange('')` (unless the value is already empty), so the prop and the
   * control agree and completing the date reports it through `onChange` again;
   * `onIncomplete` follows.
   */
  onIncomplete?: () => void;
  inputProps?: FieldInputProps;
  /** Focuses the input. */
  ref?: Ref<HTMLInputElement>;
}

/**
 * The browser's own date control (tech-015 design § 5.7), in the kit's frame. At rest
 * and empty its mask «дд.мм.гггг» and its calendar button are hidden by CSS, so an
 * empty date reads blank like its neighbours and nothing moves; hover, focus, a
 * pointer that cannot hover, `frame="always"` and a set `error` show them. These are
 * Chromium-family pseudo-elements: elsewhere the native mask stays visible and the
 * field works the same (an accepted difference, design § 8.4).
 */
export function InlineDateField({ value, onChange, onIncomplete, ...props }: InlineDateFieldProps) {
  return (
    <InputField
      {...props}
      type="date"
      value={value}
      onInputEvent={(input) => {
        if (input.validity.badInput) {
          // The control holds '' while the entry is partial: a prop left at the old date
          // is written back over what the user is typing (tech-015 review R1-1).
          if (value !== '') onChange('');
          onIncomplete?.();
        } else if (input.value !== value) {
          onChange(input.value);
        }
      }}
      kindRules={maskRules(props.frame ?? 'hover')}
    />
  );
}

const DATE = 'input[type="date"]';
const EDIT = '::-webkit-datetime-edit';
const BUTTON = '::-webkit-calendar-picker-indicator';
/** The component sets `data-empty` while the value is empty and no error is set. */
const EMPTY_AT_REST = `${DATE}[data-empty="true"]:not(:focus)`;

/**
 * The mask and the button hide until hover or focus; both stay in the layout and the
 * button stays clickable — never `display: none`. A filled date shows its text and
 * hides only the button; `frame="always"` shows both.
 */
function maskRules(frame: FieldFrameKind): CSSObject {
  if (frame === 'always') return {};
  const shown = { opacity: 1 };
  return {
    [`& ${EMPTY_AT_REST}${EDIT}`]: { opacity: 0 },
    [`& ${DATE}${BUTTON}`]: { opacity: 0 },
    [`&:hover ${EMPTY_AT_REST}${EDIT}`]: shown,
    [`&:hover ${DATE}${BUTTON}`]: shown,
    [`&:focus-within ${DATE}${BUTTON}`]: shown,
    '@media (hover: none)': {
      [`& ${EMPTY_AT_REST}${EDIT}`]: shown,
      [`& ${DATE}${BUTTON}`]: shown,
    },
  };
}
