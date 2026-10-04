import type { ReactNode } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import FilterListIcon from '@mui/icons-material/FilterList';
import FilterListOffIcon from '@mui/icons-material/FilterListOff';

export interface FilterBarProps {
  /** The registry's filter fields, in their order. */
  children: ReactNode;
  /** Whether any filter is set: «Очистить фильтры» is shown only then. */
  active: boolean;
  /** Clears every filter and shows page 1 (US-8). */
  onClear: () => void;
}

/**
 * The registry's filter band (US-8, design § 8.2): one outlined band between
 * the page header and the table, its fields in one row that wraps when the
 * window is narrower. «Очистить фильтры» stands at its right end while a
 * filter is set; the empty frame's «Сбросить фильтр» is the other way out, so
 * the two never share a name (US-8).
 */
export function FilterBar({ children, active, onClear }: FilterBarProps) {
  return (
    <Paper variant="outlined" sx={{ px: 2, py: 1.5, mb: 2 }}>
      <Stack
        role="group"
        aria-label="Фильтры"
        direction="row"
        spacing={1}
        useFlexGap
        sx={{ flexWrap: 'wrap', alignItems: 'center' }}
      >
        <FilterListIcon fontSize="small" sx={{ color: 'text.secondary' }} />
        {children}
        {active && (
          <Box sx={{ ml: 'auto' }}>
            <Button variant="text" startIcon={<FilterListOffIcon />} onClick={onClear}>
              Очистить фильтры
            </Button>
          </Box>
        )}
      </Stack>
    </Paper>
  );
}
