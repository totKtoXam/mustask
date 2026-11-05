import { memo } from 'react';
import {
  InlineDateField,
  InlineNumberField,
  InlineSelectField,
  InlineTextArea,
} from '../../../../shared/ui';
import { cardTexts } from '../../lib/cardTexts';
import {
  DATE_FIELDS,
  LIST_FIELDS,
  NOT_SET_LABEL,
  NUMBER_FIELDS,
  TEXT_FIELDS,
  TEXT_HELPER,
  kindOf,
  type ControlFieldName,
  type DateFieldName,
  type ListFieldName,
  type NumberFieldName,
} from '../../lib/fields';
import type { ElementRef } from '../../lib/fieldRegistry';
import { EARLIEST_DATE, INCOMPLETE_DATE, LATEST_DATE } from '../../lib/fieldValues';
import type { WorkItemTextFieldName } from '../../api/workItemsApi';

/** The card's texts take two rows, on the dialog and the page alike (D-4; the kit's default is three). */
export const CARD_TEXT_ROWS = 2;

/** What every control of a property section takes. */
interface PropertyProps {
  /** The value as the draft holds it (`FieldsDraft`). */
  value: string;
  /** The field's error text: a frame, a line under the field, `aria-invalid`. */
  error: string | undefined;
  /** A save is running: the read-only look, edits refused. */
  busy: boolean;
  readOnly: boolean;
  onChange: (value: string) => void;
  inputRef?: ElementRef;
}

/**
 * A field of the second column's sections (feat-022 spec § Controls and copy;
 * feat-029 design § 4.6): the kit's inline field of its kind — a fixed list as a
 * select, a number as text with its input mode, a date as the browser's own date
 * field — with its label above the value, its hint on focus and its placeholder.
 */
function PropertyFieldView({ name, ...props }: PropertyProps & { name: ControlFieldName }) {
  switch (kindOf(name)) {
    case 'list':
      return <ListProperty name={name as ListFieldName} {...props} />;
    case 'number':
      return <NumberProperty name={name as NumberFieldName} {...props} />;
    default:
      return <DateProperty name={name as DateFieldName} {...props} />;
  }
}

/**
 * A fixed list: its options only for an always-set field; «Не указан» for an
 * optional one — blank at rest, chosen to empty the field (BR-M3-31).
 */
function ListProperty({
  name,
  value,
  error,
  busy,
  readOnly,
  onChange,
  inputRef,
}: PropertyProps & {
  name: ListFieldName;
}) {
  const spec = LIST_FIELDS[name];
  return (
    <InlineSelectField<string>
      label={spec.label}
      value={value}
      options={spec.options.map((option) => ({ value: option.key, label: option.label }))}
      emptyLabel={spec.optional ? NOT_SET_LABEL : undefined}
      onChange={onChange}
      hint={spec.helper}
      error={error}
      busy={busy}
      readOnly={readOnly}
      ref={inputRef}
    />
  );
}

/** A number as typed: «,» or «.» as the separator, checked on «Сохранить» (BR-M3-32). */
function NumberProperty({
  name,
  value,
  error,
  busy,
  readOnly,
  onChange,
  inputRef,
}: PropertyProps & {
  name: NumberFieldName;
}) {
  const spec = NUMBER_FIELDS[name];
  return (
    <InlineNumberField
      label={spec.label}
      value={value}
      inputMode={spec.integer ? 'numeric' : 'decimal'}
      placeholder={spec.placeholder}
      onChange={onChange}
      hint={spec.helper}
      error={error}
      busy={busy}
      readOnly={readOnly}
      ref={inputRef}
    />
  );
}

/**
 * The browser's own date field, 2000–2099 offered. A date typed only in part has
 * no value but a bad input: the kit reports it, and the draft holds
 * `INCOMPLETE_DATE`, so «Сохранить» can say «Введите дату полностью.».
 */
function DateProperty({
  name,
  value,
  error,
  busy,
  readOnly,
  onChange,
  inputRef,
}: PropertyProps & {
  name: DateFieldName;
}) {
  const spec = DATE_FIELDS[name];
  return (
    <InlineDateField
      label={spec.label}
      value={value === INCOMPLETE_DATE ? '' : value}
      min={EARLIEST_DATE}
      max={LATEST_DATE}
      onChange={onChange}
      onIncomplete={() => onChange(INCOMPLETE_DATE)}
      hint={spec.helper}
      error={error}
      busy={busy}
      readOnly={readOnly}
      ref={inputRef}
    />
  );
}

/**
 * A text under a head of its own name (design § 4.6): «Описание» or one of the
 * type's texts. The head names it, so the text area's label is not drawn — it is
 * the head's own text, and the textbox is named by it (BR-C-20). At least two
 * rows, growing with its text (D-4).
 */
function TextBlockView({
  name,
  value,
  error,
  busy,
  readOnly,
  onChange,
  inputRef,
}: PropertyProps & {
  /** «Описание» (`description`) or a type text. */
  name: 'description' | WorkItemTextFieldName;
}) {
  const label = name === 'description' ? cardTexts.description.label : TEXT_FIELDS[name].label;
  const placeholder =
    name === 'description' ? cardTexts.description.placeholder : TEXT_FIELDS[name].placeholder;
  return (
    <InlineTextArea
      label={label}
      labelPlacement="hidden"
      minRows={CARD_TEXT_ROWS}
      placeholder={placeholder}
      value={value}
      onChange={onChange}
      hint={TEXT_HELPER}
      error={error}
      busy={busy}
      readOnly={readOnly}
      ref={inputRef}
    />
  );
}

export const PropertyField = memo(PropertyFieldView);

export const TextBlock = memo(TextBlockView);
