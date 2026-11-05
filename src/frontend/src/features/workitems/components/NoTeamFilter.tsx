import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import { useWorkContext } from '../../../shared/lib/workContext';
import { useNotice } from '../../../shared/ui';
import { NO_TEAM_FILTER_VALUE } from '../api/workItemsApi';
import { teamTexts } from '../lib/teams';

/**
 * The project list's one team control (feat-048 BR-NV-12, design § 4): «Без команды», which takes
 * `TeamFilter`'s place. It is exclusive with the team selector: the address holds one `teamId`
 * state (mod-020), so choosing «Без команды» replaces a chosen team and says
 * «Выбор сброшен: команда MARS.»; choosing a team in the selector replaces it the other way.
 */
export function NoTeamFilter({
  value,
  onChange,
}: {
  /** The `teamId` values of the address: team ids and `none`. */
  value: readonly string[];
  onChange(values: string[]): void;
}) {
  const context = useWorkContext();
  const { show } = useNotice();
  const checked = value.includes(NO_TEAM_FILTER_VALUE);
  const chosen = value.find((id) => id !== NO_TEAM_FILTER_VALUE);

  return (
    <FormControlLabel
      label={teamTexts.noTeam}
      control={
        <Checkbox
          checked={checked}
          onChange={(_event, next) => {
            if (!next) {
              onChange([]);
              return;
            }
            if (chosen !== undefined) {
              const name = context.offers?.teams.find((team) => team.id === chosen)?.name;
              show(`Выбор сброшен: команда ${name ?? ''}`.trim() + '.');
            }
            onChange([NO_TEAM_FILTER_VALUE]);
          }}
        />
      }
    />
  );
}
