import { useState, type ReactNode, type Ref } from 'react';
import Autocomplete from '@mui/material/Autocomplete';
import Checkbox from '@mui/material/Checkbox';
import InputBase from '@mui/material/InputBase';
import { useTheme } from '@mui/material/styles';
import { ThinChevronDownIcon } from '../icons/ThinIcons';
import { Tag } from '../marks/Tag';
import { FieldFrame } from './FieldFrame';
import { pickerRules } from './fieldStyles';
import type { FieldInputProps, FieldProps } from './fieldTypes';
import type { PickerInputReason } from './InlinePickerField';
import { fieldControlState, useFieldIds } from './useFieldWiring';

/** The kit's default texts of the list; the host overrides them with its own. */
const DEFAULT_LOADING_TEXT = 'Загрузка…';
const DEFAULT_NO_OPTIONS_TEXT = 'Ничего не найдено';

/** The kit's text of a chosen tag's remove button: «Убрать «Alpha»». */
function removeChosenLabel(label: string): string {
  return `Убрать «${label}»`;
}

export interface InlineMultiPickerFieldProps<O> extends FieldProps {
  options: readonly O[];
  /** The chosen options, in the order they were chosen; `[]` is none. */
  value: readonly O[];
  onChange: (options: O[]) => void;
  getOptionLabel: (option: O) => string;
  isOptionEqualToValue?: (option: O, value: O) => boolean;
  /** A stable key, when two options can share a label. */
  getOptionKey?: (option: O) => string | number;
  /** A line that is shown and cannot be chosen: a hint, «Поиск…». It has no checkbox. */
  getOptionDisabled?: (option: O) => boolean;
  /** An option's line in the list, after its checkbox; default its label. */
  renderOption?: (option: O, state: { selected: boolean }) => ReactNode;
  /** The list shows `loadingText` while it is empty; the input stays usable. */
  loading?: boolean;
  /** Default «Загрузка…». */
  loadingText?: string;
  /** Default «Ничего не найдено». */
  noOptionsText?: string;
  /** The text typed, when the host searches by it (controlled). */
  inputValue?: string;
  onInputChange?: (text: string, reason: PickerInputReason) => void;
  /** The list opens. */
  onOpen?: () => void;
  /** The list is the host's answer as it is when it returns `options` whole. */
  filterOptions?: (options: O[], state: { inputValue: string }) => O[];
  /** The name of a chosen tag's remove button; default «Убрать «<label>»». */
  removeLabel?: (label: string) => string;
  inputProps?: FieldInputProps;
  /** Focuses the input. */
  ref?: Ref<HTMLInputElement>;
}

/**
 * A search among many options that takes several (feat-028 design § 2, D-1): MUI's
 * `Autocomplete` with `multiple` on the kit's frame, as `InlinePickerField`. The chosen are
 * kit `Tag`s, each with a remove button, before the input; Backspace in an empty input removes
 * the last. The list is a multiselectable listbox that stays open while the user picks: each
 * option is an `option` with `aria-selected` and a checkbox before its line for the eye only
 * (`aria-hidden`). The box grows by rows, never in width. **Its accessible name is its label
 * only.** A read-only or busy field shows the tags without remove buttons and does not open.
 */
export function InlineMultiPickerField<O>({
  options,
  value,
  onChange,
  getOptionLabel,
  isOptionEqualToValue,
  getOptionKey,
  getOptionDisabled,
  renderOption,
  loading,
  loadingText = DEFAULT_LOADING_TEXT,
  noOptionsText = DEFAULT_NO_OPTIONS_TEXT,
  inputValue,
  onInputChange,
  onOpen,
  filterOptions,
  removeLabel = removeChosenLabel,
  inputProps,
  ref,
  ...field
}: InlineMultiPickerFieldProps<O>) {
  const theme = useTheme();
  const ids = useFieldIds(field.id);
  const state = fieldControlState(field, ids);
  // The popup's state, for the frame's `data-open`. A read-only picker shows no popup.
  const [open, setOpen] = useState(false);
  const removable = state.interactive;

  return (
    <FieldFrame
      {...field}
      ids={ids}
      empty={value.length === 0}
      hasPlaceholder={Boolean(field.placeholder)}
      multiline
      open={open && !state.locked}
      kindRules={pickerRules(theme, field.frame ?? 'hover', { wrap: true })}
    >
      <Autocomplete<O, true, true, false>
        id={ids.control}
        multiple
        disableClearable
        disableCloseOnSelect
        options={options as O[]}
        value={value as O[]}
        onChange={(_event, next) => onChange(next)}
        getOptionLabel={getOptionLabel}
        isOptionEqualToValue={isOptionEqualToValue}
        getOptionKey={getOptionKey}
        getOptionDisabled={getOptionDisabled}
        renderValue={(chosen, getItemProps) =>
          chosen.map((option, index) => {
            const { key } = getItemProps({ index });
            return (
              <Tag
                key={key}
                label={getOptionLabel(option)}
                removeLabel={removeLabel}
                onRemove={
                  removable
                    ? () => onChange(value.filter((_unused, position) => position !== index))
                    : undefined
                }
              />
            );
          })
        }
        renderOption={(props, option, optionState) => {
          const { key, ...rest } = props;
          return (
            <li key={key} {...rest}>
              {/* A line the host disabled is a hint, not a choice: it has no box. */}
              {!getOptionDisabled?.(option) && (
                <Checkbox
                  size="small"
                  checked={optionState.selected}
                  disableRipple
                  aria-hidden="true"
                  slotProps={{ input: { tabIndex: -1, 'aria-hidden': true } }}
                  sx={{ p: 0, mr: 1 }}
                />
              )}
              {renderOption
                ? renderOption(option, { selected: optionState.selected })
                : getOptionLabel(option)}
            </li>
          );
        }}
        loading={loading}
        loadingText={loadingText}
        noOptionsText={noOptionsText}
        inputValue={inputValue}
        onInputChange={(_event, text, reason) => onInputChange?.(text, reason)}
        onOpen={() => {
          setOpen(true);
          onOpen?.();
        }}
        onClose={() => setOpen(false)}
        filterOptions={filterOptions}
        readOnly={state.locked}
        disabled={field.disabled}
        popupIcon={<ThinChevronDownIcon />}
        renderInput={({ slotProps: { input, htmlInput } }) => (
          <InputBase
            ref={input.ref}
            className={input.className}
            onMouseDown={input.onMouseDown}
            fullWidth
            placeholder={field.placeholder}
            inputRef={ref}
            startAdornment={input.startAdornment}
            // The legacy `inputProps`, not `slotProps.input`: only it carries `ref` into
            // InputBase's own ref, and Autocomplete needs its ref on the input element.
            inputProps={{
              ...htmlInput,
              ...inputProps,
              ...state.aria,
              'aria-describedby':
                state.aria['aria-describedby'] ?? inputProps?.['aria-describedby'],
              'aria-readonly': state.locked ? true : undefined,
            }}
            endAdornment={input.endAdornment}
          />
        )}
      />
    </FieldFrame>
  );
}
