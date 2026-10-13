import { useId } from 'react';
import type { FieldProps } from './fieldTypes';

/** The ids of a field's parts: the control, its label, its hint and its error line. */
export interface FieldIds {
  control: string;
  label: string;
  hint: string;
  error: string;
}

/** The ids of a field's parts, derived from the host's `id` or a generated one. */
export function useFieldIds(id: string | undefined): FieldIds {
  const generated = useId();
  const control = id ?? generated;
  return {
    control,
    label: `${control}-label`,
    hint: `${control}-hint`,
    error: `${control}-error`,
  };
}

/** The attributes every field's control carries, and the state they come from (design § 5.7, § 6). */
export interface FieldControlState {
  /** The control accepts edits: not read-only, not busy, not disabled. */
  interactive: boolean;
  /** The control refuses edits: read-only or busy. */
  locked: boolean;
  aria: {
    'aria-describedby': string | undefined;
    'aria-invalid': true | undefined;
    'aria-required': true | undefined;
    'aria-busy': true | undefined;
  };
}

/**
 * What the control of a field is told, from the props every field shares: the
 * description is the error line while `error` is set, else the hint; `required` is
 * `aria-required` and never the native attribute; `busy` is the read-only look with
 * `aria-busy`.
 */
export function fieldControlState(
  props: Pick<FieldProps, 'error' | 'hint' | 'required' | 'readOnly' | 'busy' | 'disabled'>,
  ids: FieldIds,
): FieldControlState {
  const locked = props.readOnly === true || props.busy === true;
  return {
    interactive: !locked && props.disabled !== true,
    locked,
    aria: {
      'aria-describedby': props.error ? ids.error : props.hint ? ids.hint : undefined,
      'aria-invalid': props.error ? true : undefined,
      'aria-required': props.required ? true : undefined,
      'aria-busy': props.busy ? true : undefined,
    },
  };
}
