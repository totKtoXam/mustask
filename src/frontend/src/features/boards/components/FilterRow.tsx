import type { ReactNode } from 'react';
import Stack from '@mui/material/Stack';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';
import { ActionButton } from '../../../shared/ui';

/**
 * The filter row of a backlog or a board (mod-017 US-5; design § 6.2; feat-040 design § 4.1), one
 * component over the dimension the view is filtered by: the team on the project's views, the
 * project on a team's. A row of its own, 20 px below the tab row, in a group named «Фильтры» — the
 * field, passed as `children`, and, while a value is chosen, «Очистить фильтры». The chosen values
 * belong to the page, above the level, and live in its address (mod-020; feat-040 BR-TS-8).
 */
export function FilterRow({
  active,
  onClear,
  children,
}: {
  /** The address carries a value, an invalid one included: the filter is on and can be cleared. */
  active: boolean;
  onClear(): void;
  children: ReactNode;
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
      {children}
      {active && (
        <ActionButton
          tone="subtle"
          label="Очистить фильтры"
          icon={<FilterListOffIcon />}
          onClick={onClear}
        />
      )}
    </Stack>
  );
}
