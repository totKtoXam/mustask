import { useMemo } from 'react';
import Box from '@mui/material/Box';
import { InlineMultiPickerField } from '../../../shared/ui';
import { useAssigneeFilterOptions } from '../hooks/queries';
import { NOBODY_CHOICE, assigneeChoices, type AssigneeChoice } from '../lib/assigneeFilterOptions';
import { filterTexts } from '../lib/workItemFilter';

const ONLY_NOBODY: readonly AssigneeChoice[] = [NOBODY_CHOICE];

/**
 * «Исполнитель» of the list's band (feat-003 US-2, BR-WS-6..-8; design § 2.3, D-1 A): the kit's
 * multi-picker over the people assigned to the project's items — members, former members and
 * disabled accounts alike — then «Не назначен». The user types to narrow a list that can hold
 * 2 000 people; the chosen are tags with a remove button. Each option reads as `userNameWithMarks`
 * (mod-005's one helper), the same words as the list's column; options that read alike are told
 * apart in the text itself (D-2). The list is whole, in the read's order: the panel never sorts.
 *
 * `states` are the chosen states, sent to the options' read: the server decides whether the people
 * of deleted items are offered (BR-WS-6). The previous options stay while the new ones come; a
 * chosen person who is no longer an option leaves the tags, and the address keeps the id.
 *
 * The options are read when the field is drawn and the field works meanwhile: its list says
 * «Загрузка…» or, when the read failed, «Не удалось загрузить исполнителей. Попробуйте ещё раз.»,
 * and opening it again reads again. An id in the address that is no option is kept, is no tag and
 * is not named: while nothing else is chosen the field reads «Фильтр задан» (D-4).
 */
export function AssigneeFilter({
  organizationId,
  projectId,
  value,
  states,
  onChange,
}: {
  organizationId: string;
  projectId: string;
  /** The chosen user ids and `none`, as the address holds them; empty is «Все исполнители». */
  value: readonly string[];
  /** The chosen state names, which decide whose options are offered. */
  states: readonly string[];
  onChange: (ids: string[]) => void;
}) {
  const options = useAssigneeFilterOptions(organizationId, projectId, states);
  const failed = options.isError && !options.isFetching;
  const choices = useMemo(
    () => (options.isSuccess ? assigneeChoices(options.data) : []),
    [options.isSuccess, options.data],
  );
  // «Не назначен» is a constant, so its tag stands before the read has answered.
  const known = choices.length > 0 ? choices : ONLY_NOBODY;
  // One object per value: MUI resets the typed text whenever `value` holds a new object.
  const tags = useMemo(
    () => value.flatMap((id) => known.find((choice) => choice.value === id) ?? []),
    [value, known],
  );

  function change(next: AssigneeChoice[]) {
    const chosen = next.map((choice) => choice.value);
    const shown = new Set(tags.map((tag) => tag.value));
    // An id no option names stays; a tag stays only while it is still chosen.
    const kept = value.filter((id) => !shown.has(id) || chosen.includes(id));
    onChange([...kept, ...chosen.filter((id) => !kept.includes(id))]);
  }

  return (
    <Box sx={(theme) => ({ width: theme.spacing(35), maxWidth: '100%' })}>
      <InlineMultiPickerField<AssigneeChoice>
        label={filterTexts.assigneeLabel}
        frame="always"
        emptyText={
          value.length > 0 && tags.length === 0 ? filterTexts.set : filterTexts.allAssignees
        }
        options={choices}
        value={tags}
        onChange={change}
        getOptionLabel={(choice) => choice.label}
        getOptionKey={(choice) => choice.value}
        isOptionEqualToValue={(choice, chosen) => choice.value === chosen.value}
        loading={!options.isSuccess && !failed}
        loadingText={filterTexts.loading}
        noOptionsText={failed ? filterTexts.assigneeFailed : filterTexts.noAssignees}
        onOpen={() => {
          if (options.isError) {
            void options.refetch();
          }
        }}
      />
    </Box>
  );
}
