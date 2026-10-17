import { useImperativeHandle, useRef, useState, type Ref } from 'react';
import Checkbox from '@mui/material/Checkbox';
import InputBase from '@mui/material/InputBase';
import MenuItem from '@mui/material/MenuItem';
import Select from '@mui/material/Select';
import { useTheme } from '@mui/material/styles';
import { ThinChevronDownIcon } from '../icons/ThinIcons';
import { TruncatedText } from '../text/TruncatedText';
import { FieldFrame } from './FieldFrame';
import { selectRules } from './fieldStyles';
import type { SelectOption } from './InlineSelectField';
import type { FieldInputProps, FieldProps } from './fieldTypes';
import { fieldControlState, useFieldIds } from './useFieldWiring';

/** The kit's default texts of the list; the host overrides them with its own. */
const DEFAULT_LOADING_TEXT = 'Загрузка…';
const DEFAULT_NO_OPTIONS_TEXT = 'Ничего не найдено';
/** The names in the value are joined by this. */
const NAME_SEPARATOR = ', ';

export interface InlineMultiSelectFieldProps<V extends string> extends FieldProps {
  /** The chosen values; `[]` is none (and `emptyText` shows at rest). */
  value: readonly V[];
  onChange: (values: V[]) => void;
  options: readonly SelectOption<V>[];
  /** The list shows `loadingText` while it has no option; the field itself works. */
  loading?: boolean;
  /** Default «Загрузка…». */
  loadingText?: string;
  /** What the list says when it has no option and nothing is loading; default «Ничего не найдено». */
  noOptionsText?: string;
  /** The list opens. */
  onOpen?: () => void;
  inputProps?: FieldInputProps;
  /** The combobox: focus it. */
  ref?: Ref<HTMLElement>;
}

/**
 * A choice of several among a few options (mod-017 design § 2): MUI's `Select` with
 * `multiple` on the kit's frame, as `InlineSelectField`. The list is a multiselectable
 * listbox; each option is an `option` with `aria-selected`, a checkbox before its label
 * for the eye only (`aria-hidden`, no tab stop). It stays open while the user ticks;
 * Escape or a click outside closes it and the focus stays on the field. The value is the
 * chosen names joined by «, » and cut to one line; a chosen value no option names is not
 * shown (it stays in `value`, selecting whatever it selects). **Its accessible name is its
 * label only.** A read-only field does not open.
 */
export function InlineMultiSelectField<V extends string>({
  value,
  onChange,
  options,
  loading = false,
  loadingText = DEFAULT_LOADING_TEXT,
  noOptionsText = DEFAULT_NO_OPTIONS_TEXT,
  onOpen,
  inputProps,
  ref,
  ...field
}: InlineMultiSelectFieldProps<V>) {
  const theme = useTheme();
  const ids = useFieldIds(field.id);
  const state = fieldControlState(field, ids);
  const rootRef = useRef<HTMLDivElement>(null);
  const combobox = () => rootRef.current?.querySelector<HTMLElement>('[role="combobox"]') ?? null;
  useImperativeHandle(
    ref,
    () => rootRef.current?.querySelector<HTMLElement>('[role="combobox"]') as HTMLElement,
    [],
  );
  // The menu takes the focus away while it is open; the frame says so by `data-open`.
  const [open, setOpen] = useState(false);

  const names = options
    .filter((option) => value.includes(option.value))
    .map((option) => option.label)
    .join(NAME_SEPARATOR);

  return (
    <FieldFrame
      {...field}
      ids={ids}
      empty={names === ''}
      open={open}
      // A label cannot name a div; it moves focus to the combobox itself.
      labelFor={null}
      onLabelClick={() => combobox()?.focus()}
      kindRules={selectRules(theme, field.frame ?? 'hover')}
    >
      <Select
        ref={rootRef}
        multiple
        value={[...value]}
        onOpen={() => {
          setOpen(true);
          onOpen?.();
        }}
        onClose={() => setOpen(false)}
        onChange={(event) => {
          const next = event.target.value;
          onChange((typeof next === 'string' ? next.split(',') : next) as V[]);
        }}
        variant="standard"
        input={<InputBase />}
        labelId={ids.label}
        readOnly={state.locked}
        disabled={field.disabled}
        IconComponent={ThinChevronDownIcon}
        renderValue={() => (names === '' ? '' : <TruncatedText>{names}</TruncatedText>)}
        SelectDisplayProps={{
          ...inputProps,
          id: ids.control,
          'aria-labelledby': ids.label,
          ...state.aria,
        }}
      >
        {options.length === 0 && (
          <MenuItem disabled value="">
            {loading ? loadingText : noOptionsText}
          </MenuItem>
        )}
        {options.map((option) => (
          <MenuItem key={option.value} value={option.value}>
            <Checkbox
              size="small"
              checked={value.includes(option.value)}
              disableRipple
              aria-hidden="true"
              slotProps={{ input: { tabIndex: -1, 'aria-hidden': true } }}
              sx={{ p: 0, mr: 1 }}
            />
            {option.label}
          </MenuItem>
        ))}
      </Select>
    </FieldFrame>
  );
}
