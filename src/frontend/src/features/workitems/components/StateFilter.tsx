import Box from '@mui/material/Box';
import { useProcessDefinition } from '../../process';
import { InlineMultiSelectField } from '../../../shared/ui';
import { filterTexts, stateOptions } from '../lib/workItemFilter';

/**
 * «Статус» of the list's band (feat-003 US-1, BR-WS-3; design § 2.2): the kit's
 * `InlineMultiSelectField` over the state names of the organization's process, each once, in the
 * order they first appear across its types — names only, no counts, and no list of "deleted names":
 * it sends the names chosen and the server judges by category (BR-WS-4). «Удалена» is among them.
 *
 * The process is read when the field is drawn (the page reads it too, so it is one read) and the
 * field works meanwhile: its list says «Загрузка…», or, when the read has failed, «Не удалось
 * загрузить статусы. Попробуйте ещё раз.», and opening it again reads again. A name in the address
 * that is no option (`готово`, `новая`) is kept, selects nothing and is not named: while nothing
 * else is chosen the field reads «Фильтр задан» in place of «Все статусы» (D-4).
 */
export function StateFilter({
  organizationId,
  value,
  onChange,
}: {
  organizationId: string;
  /** The chosen state names as the address holds them; empty is «Все статусы». */
  value: readonly string[];
  onChange: (names: string[]) => void;
}) {
  const process = useProcessDefinition(organizationId);
  const failed = process.isError && !process.isFetching;

  return (
    <Box sx={(theme) => ({ width: theme.spacing(25), maxWidth: '100%' })}>
      <InlineMultiSelectField<string>
        label={filterTexts.stateLabel}
        frame="always"
        emptyText={value.length > 0 ? filterTexts.set : filterTexts.allStates}
        value={value}
        options={process.isSuccess ? stateOptions(process.data) : []}
        loading={!process.isSuccess && !failed}
        loadingText={filterTexts.loading}
        noOptionsText={failed ? filterTexts.stateFailed : filterTexts.noMatch}
        onOpen={() => {
          if (process.isError) {
            void process.refetch();
          }
        }}
        onChange={onChange}
      />
    </Box>
  );
}
