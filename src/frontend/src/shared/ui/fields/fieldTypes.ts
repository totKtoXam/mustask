import type { AriaAttributes, InputHTMLAttributes, ReactNode } from 'react';
import type { DataAttributes } from '../passThrough';

/** Where the label stands: above the value, at its left (the band's rows), or not drawn. */
export type LabelPlacement = 'top' | 'start' | 'hidden';

/** The value's type: the body text, the card's title, or a table cell's. */
export type FieldSize = 'body' | 'title' | 'table';

/** `hover`: no frame at rest (the card). `always`: a drawn frame (a form that starts blank). */
export type FieldFrameKind = 'hover' | 'always';

/** Where a start adornment stands: inside the box before the text, or in the label column. */
export type AdornmentPlacement = 'value' | 'label';

/**
 * What every field of the family takes (tech-015 design § 5.7): one anatomy — a
 * label, the value, a hint, an error, the same states, the same names. The
 * component-specific props are added by each field.
 */
export interface FieldProps {
  /** Always set: it names the control, drawn or not. */
  label: string;
  /** Default `top`. `hidden`: the title, the assignee, a text under a head of its own name. */
  labelPlacement?: LabelPlacement;
  /**
   * `start` only. The label column, default `layout.card.bandLabelWidth`: the value's
   * box starts where it ends and its text `layout.field.textInset` further in.
   */
  labelWidth?: number;
  /** Default `body`. */
  size?: FieldSize;
  /** Default `hover` — the card. `always` is the form look: a 1 px frame at rest. */
  frame?: FieldFrameKind;
  /** A caption on the label's line while the field has focus; in the description unless the error line replaces it. */
  hint?: string;
  /** The text; the error frame, the line under the box and `aria-invalid`. */
  error?: string;
  /** An asterisk and `aria-required` — never the native attribute. */
  required?: boolean;
  readOnly?: boolean;
  /** The read-only look, the text not greyed, `aria-busy`, edits refused. */
  busy?: boolean;
  /** Only where the value can never be edited: native, `text.disabled`. */
  disabled?: boolean;
  /** Always the input's own `placeholder` attribute; only its look follows the states. */
  placeholder?: string;
  /** Shown at rest when the field is empty, in `text.secondary`. */
  emptyText?: string;
  /** A dot, an icon or a 24 px avatar. */
  startAdornment?: ReactNode;
  /** Default `label` when `labelPlacement="start"`, else `value`. */
  adornmentPlacement?: AdornmentPlacement;
  id?: string;
}

/**
 * What a host may still hand to the input itself (tech-015 design § 5.7): the
 * keyboard hints, the key and focus handlers, `aria-*` and `data-*`. The kit sets
 * the name, the description, `aria-invalid`, `aria-required` and `aria-busy` itself;
 * a field takes no `sx`, `className` or width.
 */
export type FieldInputProps = AriaAttributes &
  DataAttributes &
  Pick<
    InputHTMLAttributes<HTMLInputElement>,
    | 'inputMode'
    | 'autoComplete'
    | 'name'
    | 'enterKeyHint'
    | 'spellCheck'
    | 'onKeyDown'
    | 'onKeyUp'
    | 'onFocus'
    | 'onBlur'
    | 'onPaste'
  >;
