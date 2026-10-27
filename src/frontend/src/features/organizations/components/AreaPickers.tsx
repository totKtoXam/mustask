import { useMemo, useState, type ReactNode, type Ref } from 'react';
import { InlineMultiPickerField, InlinePickerField } from '../../../shared/ui';
import { narrow } from '../lib/areaTree';
import { PICKER_OPTION_LIMIT, areaLinkDialog, areasField } from '../lib/areaTexts';

/**
 * The searchable pickers of the area workspace (mod-018 design D-1): a local filter over a list the
 * host already holds. Typed text narrows by the option's texts, the list draws at most
 * `PICKER_OPTION_LIMIT` matches and ends with «Показаны первые 50 из 312. Уточните запрос.» when
 * there are more; no route and no wait per keystroke. The hint line is shown and cannot be chosen.
 */

type Entry<O> = { kind: 'item'; item: O } | { kind: 'more'; text: string };

interface CommonProps<O> {
  label: string;
  hint?: string;
  error?: string;
  required?: boolean;
  busy?: boolean;
  /** The choices on offer. */
  options: readonly O[];
  /** The list is still being read. */
  loading?: boolean;
  /** The text of an empty list (all linked, none exist, the read failed); absent: the kit's default. */
  emptyText?: string;
  getKey(option: O): string;
  getLabel(option: O): string;
  /** More texts a typed query matches, e.g. a project's key. */
  matchTexts?(option: O): readonly string[];
  /** The line in the list, when it is more than the label. */
  renderOption?(option: O): ReactNode;
  onOpen?(): void;
}

function entriesOf<O>(
  options: readonly O[],
  typed: string,
  getLabel: (option: O) => string,
  matchTexts: ((option: O) => readonly string[]) | undefined,
): Entry<O>[] {
  const { shown, total } = narrow(options, typed, (option) => [
    getLabel(option),
    ...(matchTexts?.(option) ?? []),
  ]);
  const list: Entry<O>[] = shown.map((item) => ({ kind: 'item', item }));
  if (total > PICKER_OPTION_LIMIT) {
    list.push({ kind: 'more', text: areaLinkDialog.more(PICKER_OPTION_LIMIT, total) });
  }
  return list;
}

/** Several choices (the link dialogs, the creation forms). */
export function LocalMultiPicker<O>({
  value,
  onChange,
  ...props
}: CommonProps<O> & { value: readonly O[]; onChange(next: O[]): void }) {
  const [typed, setTyped] = useState('');
  const entries = entriesOf(props.options, typed, props.getLabel, props.matchTexts);
  // Stable between renders: a new array each time reads as a changed value to the autocomplete, which
  // then resets the typed text on every keystroke.
  const chosen = useMemo<Entry<O>[]>(() => value.map((item) => ({ kind: 'item', item })), [value]);
  const known = [
    ...entries,
    ...chosen.filter(
      (entry) =>
        entry.kind === 'item' &&
        !entries.some(
          (other) => other.kind === 'item' && props.getKey(other.item) === props.getKey(entry.item),
        ),
    ),
  ];

  return (
    <InlineMultiPickerField<Entry<O>>
      label={props.label}
      frame="always"
      hint={props.hint}
      error={props.error}
      required={props.required}
      busy={props.busy}
      value={chosen}
      options={known}
      loading={props.loading}
      inputValue={typed}
      onInputChange={(text, reason) => setTyped(reason === 'input' ? text : '')}
      onOpen={props.onOpen}
      filterOptions={() => entries}
      noOptionsText={typed === '' ? props.emptyText : undefined}
      onChange={(next) =>
        onChange(next.flatMap((entry) => (entry.kind === 'item' ? [entry.item] : [])))
      }
      getOptionLabel={(entry) => (entry.kind === 'item' ? props.getLabel(entry.item) : entry.text)}
      getOptionKey={(entry) => (entry.kind === 'item' ? props.getKey(entry.item) : '__more__')}
      getOptionDisabled={(entry) => entry.kind === 'more'}
      isOptionEqualToValue={(first, second) =>
        first.kind === 'item' &&
        second.kind === 'item' &&
        props.getKey(first.item) === props.getKey(second.item)
      }
      renderOption={(entry) =>
        entry.kind === 'more'
          ? entry.text
          : (props.renderOption?.(entry.item) ?? props.getLabel(entry.item))
      }
    />
  );
}

/** One choice (the parent of an area, the destination of a move). */
export function LocalPicker<O>({
  value,
  onChange,
  placeholder,
  inputRef,
  ...props
}: CommonProps<O> & {
  value: O | null;
  onChange(next: O): void;
  placeholder?: string;
  /** Focuses the input when the dialog opens. */
  inputRef?: Ref<HTMLInputElement>;
}) {
  const [typed, setTyped] = useState('');
  const entries = entriesOf(props.options, typed, props.getLabel, props.matchTexts);
  const chosen = useMemo<Entry<O> | null>(
    () => (value ? { kind: 'item', item: value } : null),
    [value],
  );

  return (
    <InlinePickerField<Entry<O>>
      label={props.label}
      frame="always"
      hint={props.hint}
      error={props.error}
      required={props.required}
      busy={props.busy}
      placeholder={placeholder}
      value={chosen}
      options={
        chosen && !entries.some((entry) => sameEntry(entry, chosen, props.getKey))
          ? [chosen, ...entries]
          : entries
      }
      loading={props.loading}
      onInputChange={(text, reason) => setTyped(reason === 'input' ? text : '')}
      onOpen={props.onOpen}
      filterOptions={() => entries}
      noOptionsText={typed === '' ? props.emptyText : undefined}
      clearable={false}
      ref={inputRef}
      onChange={(next) => {
        if (next && next.kind === 'item') {
          onChange(next.item);
        }
      }}
      getOptionLabel={(entry) => (entry.kind === 'item' ? props.getLabel(entry.item) : entry.text)}
      getOptionKey={(entry) => (entry.kind === 'item' ? props.getKey(entry.item) : '__more__')}
      getOptionDisabled={(entry) => entry.kind === 'more'}
      isOptionEqualToValue={(first, second) => sameEntry(first, second, props.getKey)}
      renderOption={(entry) =>
        entry.kind === 'more'
          ? entry.text
          : (props.renderOption?.(entry.item) ?? props.getLabel(entry.item))
      }
    />
  );
}

function sameEntry<O>(first: Entry<O>, second: Entry<O>, getKey: (option: O) => string): boolean {
  return (
    first.kind === 'item' && second.kind === 'item' && getKey(first.item) === getKey(second.item)
  );
}

/** The area an «Области» field offers: its path is its name. */
export interface AreaOption {
  id: string;
  path: string;
}

/**
 * «Области» of the project and team creation forms (mod-018 design § 5.3): the organization's areas by
 * path, chosen several at a time over the list the form read when it opened.
 */
export function AreasField({
  hint,
  error,
  busy,
  value,
  options,
  loading,
  failed,
  onChange,
}: {
  hint: string;
  error?: string;
  busy: boolean;
  value: readonly AreaOption[];
  options: readonly AreaOption[];
  loading: boolean;
  failed: boolean;
  onChange(next: AreaOption[]): void;
}) {
  return (
    <LocalMultiPicker<AreaOption>
      label={areasField.label}
      hint={hint}
      busy={busy}
      error={error}
      options={options}
      loading={loading}
      emptyText={failed ? areaLinkDialog.projectAreas.loadFailed : areasField.none}
      value={value}
      onChange={onChange}
      getKey={(option) => option.id}
      getLabel={(option) => option.path}
    />
  );
}
