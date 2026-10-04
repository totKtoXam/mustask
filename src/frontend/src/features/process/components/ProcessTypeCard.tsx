import { useId } from 'react';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import type { WorkItemType } from '../api/processApi';
import { CategoryCell } from './CategoryCell';
import { STATE_COLUMNS } from './stateColumns';
import { TypeMark } from './TypeMark';

function Fact({ term, value }: { term: string; value: string }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1 }}>
      <Typography component="dt" variant="body2" color="text.secondary">
        {term}
      </Typography>
      <Typography component="dd" variant="body2" sx={{ m: 0 }}>
        {value}
      </Typography>
    </Box>
  );
}

/**
 * One work item type (design DD-1, layout B): a `section` named by its `h2`
 * — the type mark, then the name — the facts «Ключ» and «Уровень бэклога» as
 * a description list, and the table of its states in the order received.
 * Read-only: no control, and the «Начальный» chip never takes focus.
 */
export function ProcessTypeCard({ type }: { type: WorkItemType }) {
  const headingId = useId();

  return (
    <Paper component="section" aria-labelledby={headingId} sx={{ p: 2, height: '100%' }}>
      <Typography
        variant="h2"
        component="h2"
        id={headingId}
        sx={{ display: 'flex', alignItems: 'center', gap: 1, overflowWrap: 'anywhere' }}
      >
        <TypeMark appearance={type.appearance} />
        {type.name}
      </Typography>
      <Stack
        component="dl"
        direction="row"
        useFlexGap
        sx={{ flexWrap: 'wrap', columnGap: 3, rowGap: 0.5, mt: 1, mb: 2 }}
      >
        <Fact term="Ключ" value={type.key} />
        <Fact term="Уровень бэклога" value={type.backlogLevel.name} />
      </Stack>
      <TableContainer>
        <Table size="small" aria-label={`Статусы типа «${type.name}»`}>
          <TableHead>
            <TableRow>
              {STATE_COLUMNS.map((column) => (
                <TableCell key={column} scope="col">
                  {column}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {type.states.map((state) => (
              <TableRow key={state.id}>
                <TableCell>{state.order}</TableCell>
                <TableCell component="th" scope="row">
                  {state.name}
                  {state.isInitial && (
                    <Chip size="small" variant="outlined" label="Начальный" sx={{ ml: 1 }} />
                  )}
                </TableCell>
                <CategoryCell category={state.category} />
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Paper>
  );
}
