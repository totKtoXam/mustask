import { memo, useState, type Ref } from 'react';
import { useTeams } from '../../../organizations';
import type { WorkItemTeam } from '../../api/workItemsApi';
import { TeamField } from '../TeamField';

/**
 * «Команда» of the band (mod-017 US-1; design § 3): block B, row 2, under «Область». The
 * project's teams are read when the list first opens, as «Область»'s tree is: until they
 * arrive the list says «Загрузка…»; a failed read says so in the list, and opening it again
 * reads again (so does a refusal of the save — «Команда не найдена.» — at the next opening).
 */
function CardTeamFieldView({
  organizationId,
  projectId,
  value,
  error,
  busy,
  readOnly,
  onChange,
  inputRef,
}: {
  organizationId: string;
  projectId: string;
  value: WorkItemTeam | null;
  error: string | undefined;
  busy: boolean;
  readOnly: boolean;
  onChange: (team: WorkItemTeam | null) => void;
  inputRef?: Ref<HTMLInputElement>;
}) {
  const [opened, setOpened] = useState(false);
  const teams = useTeams(organizationId, projectId, { enabled: opened });
  const failed = teams.isError && !teams.isFetching;

  return (
    <TeamField
      value={value}
      teams={teams.isSuccess ? teams.data : undefined}
      loading={opened && !teams.isSuccess && !failed}
      failed={failed}
      onOpen={() => {
        setOpened(true);
        if (teams.isError || (teams.isSuccess && error !== undefined)) {
          void teams.refetch();
        }
      }}
      onChange={onChange}
      error={error}
      busy={busy}
      readOnly={readOnly}
      inputRef={inputRef}
    />
  );
}

export const CardTeamField = memo(CardTeamFieldView);
