import { useState, type ReactNode, type Ref } from 'react';
import Autocomplete from '@mui/material/Autocomplete';
import Box from '@mui/material/Box';
import InputBase from '@mui/material/InputBase';
import { useTheme, type CSSObject, type Theme } from '@mui/material/styles';
import { ThinChevronDownIcon, ThinCloseIcon } from '../icons/ThinIcons';
import { FieldFrame } from './FieldFrame';
import { OPEN } from './fieldStyles';
import type { FieldFrameKind, FieldInputProps, FieldProps } from './fieldTypes';
import { fieldControlState, useFieldIds } from './useFieldWiring';

/** The kit's default texts of a picker's list; the host overrides them with its own. */
const DEFAULT_LOADING_TEXT = 'Загрузка…';
const DEFAULT_NO_OPTIONS_TEXT = 'Ничего не найдено';

/** Why the typed text changed: typed (`input`), or set by the list's own doing. */
export type PickerInputReason =
  'input' | 'reset' | 'clear' | 'blur' | 'selectOption' | 'removeOption';

export interface InlinePickerFieldProps<O> extends FieldProps {
  options: readonly O[];
  value: O | null;
  onChange: (option: O | null) => void;
  getOptionLabel: (option: O) => string;
  isOptionEqualToValue?: (option: O, value: O) => boolean;
  /** A stable key, when two options can share a label. */
  getOptionKey?: (option: O) => string | number;
  /** A line that is shown and cannot be chosen: a hint, «Поиск…». */
  getOptionDisabled?: (option: O) => boolean;
  /** An option's line in the list; default its label. */
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
  /** A clear button after the value; default none — a value is replaced, not emptied. */
  clearable?: boolean;
  /** A link or a button after the value — «Открыть MSK-FEAT-6». */
  endAction?: ReactNode;
  inputProps?: FieldInputProps;
  /** Focuses the input. */
  ref?: Ref<HTMLInputElement>;
}

/**
 * A search among many options (tech-015 design § 5.7): MUI's `Autocomplete` on the
 * kit's frame. The input is always there: at rest it shows the chosen option's text,
 * on focus the text is selected for searching. The popup's arrow is the thin chevron
 * and shows with the frame; a read-only picker does not open.
 */
export function InlinePickerField<O>({
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
  clearable = false,
  endAction,
  inputProps,
  ref,
  ...field
}: InlinePickerFieldProps<O>) {
  const theme = useTheme();
  const ids = useFieldIds(field.id);
  const state = fieldControlState(field, ids);
  // MUI types the clear button's absence in the component's generics; the kit's prop is a boolean.
  const clearProps = (clearable ? {} : { disableClearable: true }) as object;
  // The popup's state, for the frame's `data-open`. A read-only picker shows no popup.
  const [open, setOpen] = useState(false);

  return (
    <FieldFrame
      {...field}
      ids={ids}
      empty={value === null}
      hasPlaceholder={Boolean(field.placeholder)}
      open={open && !state.locked}
      kindRules={pickerRules(theme, field.frame ?? 'hover')}
    >
      <Autocomplete<O, false, false>
        id={ids.control}
        {...clearProps}
        options={options as O[]}
        value={value}
        onChange={(_event, next) => onChange(next)}
        getOptionLabel={getOptionLabel}
        isOptionEqualToValue={isOptionEqualToValue}
        getOptionKey={getOptionKey}
        getOptionDisabled={getOptionDisabled}
        renderOption={
          renderOption
            ? (props, option, optionState) => {
                const { key, ...rest } = props;
                return (
                  <li key={key} {...rest}>
                    {renderOption(option, { selected: optionState.selected })}
                  </li>
                );
              }
            : undefined
        }
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
        clearIcon={<ThinCloseIcon />}
        renderInput={({ slotProps: { input, htmlInput } }) => (
          <InputBase
            ref={input.ref}
            className={input.className}
            onMouseDown={input.onMouseDown}
            fullWidth
            placeholder={field.placeholder}
            inputRef={ref}
            // The legacy `inputProps`, not `slotProps.input`: only it carries `ref` into
            // InputBase's own ref, and Autocomplete needs its ref on the input element.
            inputProps={{
              ...htmlInput,
              ...inputProps,
              ...state.aria,
              // The field's own description (its error or hint) wins; a caption the host
              // draws under the field (the creation dialog's backlog line) is the fallback.
              'aria-describedby':
                state.aria['aria-describedby'] ?? inputProps?.['aria-describedby'],
              'aria-readonly': state.locked ? true : undefined,
            }}
            endAdornment={
              <>
                {endAction && (
                  <Box component="span" sx={{ display: 'inline-flex', flexShrink: 0, ml: 1 }}>
                    {endAction}
                  </Box>
                )}
                {input.endAdornment}
              </>
            }
          />
        )}
      />
    </FieldFrame>
  );
}

/**
 * The popup arrow and the clear button are small, in the flow, and shown with the frame.
 * Each state's key repeats the state before *each* indicator and names the root once (`&`):
 * a second `&`, or an indicator left without its state, is a rule that never matches or
 * always does.
 */
function pickerRules(theme: Theme, frame: FieldFrameKind): CSSObject {
  const popup = '.MuiAutocomplete-popupIndicator';
  const clear = '.MuiAutocomplete-clearIndicator';
  const indicators = `&& ${popup}, && ${clear}`;
  const when = (state: string) => `&${state} ${popup}, &${state} ${clear}`;
  const shown = { opacity: 1 };
  return {
    '&&& .MuiAutocomplete-inputRoot': { padding: 0, flexWrap: 'nowrap' },
    '&&& .MuiAutocomplete-input': { padding: 0, minWidth: 0, width: 0, flexGrow: 1 },
    '&&& .MuiAutocomplete-endAdornment': {
      position: 'static',
      transform: 'none',
      display: 'inline-flex',
      alignItems: 'center',
      flexShrink: 0,
      gap: '2px',
    },
    [indicators]: {
      width: theme.layout.iconAction.row.box,
      height: theme.layout.iconAction.row.box,
      padding: 0,
      margin: 0,
      fontSize: theme.layout.iconAction.chevron.glyph,
      color: theme.palette.text.primary,
      opacity: frame === 'always' ? 1 : 0,
    },
    '&& .MuiAutocomplete-popupIndicator, && .MuiAutocomplete-popupIndicatorOpen': {
      transform: 'none',
    },
    [when(':hover')]: shown,
    [when(':focus-within')]: shown,
    [when(OPEN)]: shown,
    '@media (hover: none)': { [indicators]: shown },
  };
}
