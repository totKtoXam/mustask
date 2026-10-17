import Box from '@mui/material/Box';
import { useTeams } from '../../organizations';
import { InlineMultiSelectField } from '../../../shared/ui';
import { filterOptions, teamTexts } from '../lib/teams';

/**
 * «Команда» of the filter (mod-017 US-5, BR-OT-8; design § 5.2): the kit’s
 * `InlineMultiSelectField` over the project’s teams by name, then «Без команды» — whose value is
 * the contract’s `none` — with no counts. The same field on the project’s list and on the
 * project’s backlog and board; the page keeps the chosen values (`string[]`) and sends them as
 * `teamId` (api-contract § 3). Any chosen value matches.
 *
 * The teams are read when the field is drawn and the field works meanwhile: its list says
 * «Загрузка…», or, when the read has failed, «Не удалось загрузить команды. Попробуйте ещё
 * раз.», and opening it again reads again. A chosen team that was deleted meanwhile stays in
 * the filter and selects nothing; the field names only the teams it knows, and «Очистить
 * фильтры» removes it.
 */
export function TeamFilter({
  organizationId,
  projectId,
  value,
  onChange,
}: {
  organizationId: string;
  projectId: string;
  /** The chosen team ids and `none`; empty is «Все команды». */
  value: readonly string[];
  onChange: (values: string[]) => void;
}) {
  const teams = useTeams(organizationId, projectId);
  const failed = teams.isError && !teams.isFetching;

  return (
    <Box sx={(theme) => ({ width: theme.spacing(30), maxWidth: '100%' })}>
      <InlineMultiSelectField<string>
        label={teamTexts.label}
        frame="always"
        emptyText={teamTexts.allTeams}
        value={value}
        options={teams.isSuccess ? filterOptions(teams.data) : []}
        loading={!teams.isSuccess && !failed}
        loadingText={teamTexts.loading}
        noOptionsText={failed ? teamTexts.failed : teamTexts.noMatch}
        onOpen={() => {
          if (teams.isError) {
            void teams.refetch();
          }
        }}
        onChange={onChange}
      />
    </Box>
  );
}
