import type { KeyboardEvent } from 'react';
import MenuItem from '@mui/material/MenuItem';
import TextField from '@mui/material/TextField';
import {
  DATE_FIELDS,
  LIST_FIELDS,
  NOT_SET_LABEL,
  NUMBER_FIELDS,
  kindOf,
  type ControlFieldName,
  type DateFieldName,
  type ListFieldName,
  type NumberFieldName,
} from '../lib/fields';
import { EARLIEST_DATE, INCOMPLETE_DATE, LATEST_DATE } from '../lib/fieldValues';

interface ControlProps {
  /** The value as the draft holds it (`FieldsDraft`). */
  value: string;
  disabled: boolean;
  /** The field's error text: it replaces the helper, in the error colour. */
  error: string | undefined;
  onChange(value: string): void;
}

/**
 * A control of the right column's sections (feat-022 spec § Controls and
 * copy; mod-015 § 2.6): a small outlined `TextField` with its label and
 * helper, taking the column's width — a fixed list as a `select`, a number as
 * text with its input mode, a date as the browser's own date field.
 */
export function FieldControl({ name, ...props }: ControlProps & { name: ControlFieldName }) {
  switch (kindOf(name)) {
    case 'list':
      return <ListControl name={name as ListFieldName} {...props} />;
    case 'number':
      return <NumberControl name={name as NumberFieldName} {...props} />;
    default:
      return <DateControl name={name as DateFieldName} {...props} />;
  }
}

/**
 * A fixed list: its options only for an always-set field; «Не указан» first
 * for an optional one, shown as the value while it is empty (BR-M3-31).
 */
function ListControl({
  name,
  value,
  disabled,
  error,
  onChange,
}: ControlProps & { name: ListFieldName }) {
  const spec = LIST_FIELDS[name];
  return (
    <TextField
      select
      fullWidth
      label={spec.label}
      value={value}
      disabled={disabled}
      error={error !== undefined}
      helperText={error ?? spec.helper}
      onChange={(event) => onChange(event.target.value)}
      slotProps={{
        select: { displayEmpty: spec.optional },
        inputLabel: spec.optional ? { shrink: true } : undefined,
      }}
    >
      {spec.optional && <MenuItem value="">{NOT_SET_LABEL}</MenuItem>}
      {spec.options.map((option) => (
        <MenuItem key={option.key} value={option.key}>
          {option.label}
        </MenuItem>
      ))}
    </TextField>
  );
}

/** A number as typed: «,» or «.» as the separator, checked on «Сохранить» (BR-M3-32). */
function NumberControl({
  name,
  value,
  disabled,
  error,
  onChange,
}: ControlProps & { name: NumberFieldName }) {
  const spec = NUMBER_FIELDS[name];
  return (
    <TextField
      fullWidth
      label={spec.label}
      placeholder={spec.placeholder}
      value={value}
      disabled={disabled}
      error={error !== undefined}
      helperText={error ?? spec.helper}
      onChange={(event) => onChange(event.target.value)}
      slotProps={{ htmlInput: { inputMode: spec.integer ? 'numeric' : 'decimal' } }}
    />
  );
}

/**
 * The browser's own date field (status.md D-6, OQ-2 (a)), its label always
 * raised, 2000–2099 offered. A date typed only in part has no value but a bad
 * input, and the browser may announce no change for it: every key read
 * reports it too, as `INCOMPLETE_DATE`, so «Сохранить» can say «Введите дату
 * полностью.».
 */
function DateControl({
  name,
  value,
  disabled,
  error,
  onChange,
}: ControlProps & { name: DateFieldName }) {
  const spec = DATE_FIELDS[name];
  const report = (input: HTMLInputElement) => {
    const next = input.validity.badInput ? INCOMPLETE_DATE : input.value;
    if (next !== value) {
      onChange(next);
    }
  };
  return (
    <TextField
      type="date"
      fullWidth
      label={spec.label}
      value={value === INCOMPLETE_DATE ? '' : value}
      disabled={disabled}
      error={error !== undefined}
      helperText={error ?? spec.helper}
      onChange={(event) => report(event.target as HTMLInputElement)}
      slotProps={{
        inputLabel: { shrink: true },
        htmlInput: {
          min: EARLIEST_DATE,
          max: LATEST_DATE,
          onKeyUp: (event: KeyboardEvent<HTMLInputElement>) => report(event.currentTarget),
        },
      }}
    />
  );
}
