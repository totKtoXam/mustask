import Stack from '@mui/material/Stack';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import { TeamFilter } from '../../workitems';
import { ActionButton } from '../../../shared/ui';

/**
 * The project's views’ filter row (mod-017 US-5; design § 6.2): a row of its own, 20 px below
 * the tab row, in a group named «Фильтры» — the field «Команда» and, while a value is chosen,
 * «Очистить фильтры». A team’s views have none: the team is the filter. The chosen values
 * belong to the page, above the level, and live in its address (mod-020).
 */
export function ProjectFilterRow({
  organizationId,
  projectId,
  teamIds,
  onChange,
}: {
  organizationId: string;
  projectId: string;
  /** The chosen team ids and \`none\`. */
  teamIds: readonly string[];
  onChange(teamIds: string[]): void;
}) {
  return (
    <Stack
      role="group"
      aria-label="Фильтры"
      direction="row"
      spacing={1}
      useFlexGap
      sx={{ mt: 2.5, mb: 2, flexWrap: 'wrap', alignItems: 'flex-end' }}
    >
      <TeamFilter
        organizationId={organizationId}
        projectId={projectId}
        value={teamIds}
        onChange={onChange}
      />
      {teamIds.length > 0 && (
        <ActionButton
          tone="subtle"
          label="Очистить фильтры"
          icon={<FilterListOffIcon />}
          onClick={() => onChange([])}
        />
      )}
    </Stack>
  );
}
