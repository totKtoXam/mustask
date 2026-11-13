import CheckOutlinedIcon from '@mui/icons-material/CheckOutlined';
import Box from '@mui/material/Box';
import { Tag } from '../../../shared/ui';
import type { SprintState } from '../api/iterationsApi';
import { sprintTexts } from '../lib/sprintTexts';

/**
 * A sprint's state (design § 3.2): neutral «Запланирован», accent «Активен», neutral with a check
 * glyph «Завершён» — the state is always a word, never colour alone.
 */
export function SprintStateTag({ state }: { state: SprintState }) {
  const label = sprintTexts.list.states[state];
  if (state === 'Closed') {
    return (
      <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
        <CheckOutlinedIcon aria-hidden="true" sx={{ fontSize: 16 }} />
        <Tag label={label} />
      </Box>
    );
  }
  return <Tag label={label} tone={state === 'Active' ? 'accent' : 'neutral'} />;
}
