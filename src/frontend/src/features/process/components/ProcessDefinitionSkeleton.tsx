import Box from '@mui/material/Box';
import Grid from '@mui/material/Grid';
import Paper from '@mui/material/Paper';
import Skeleton from '@mui/material/Skeleton';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import { STATE_COLUMNS } from './stateColumns';

/** A process always has five types (BR-M2-8); the skeleton has as many cards. */
const SKELETON_CARDS = 5;
const SKELETON_ROWS = 4;

/**
 * Loading (design § States; mod-015 BR-12): one status region named
 * «Загрузка данных» around the same grid as the content — five cards, each a
 * heading line and a facts line, then four placeholder rows under the real
 * column headers. No spinner and no part of the process. The pulse stops
 * under `prefers-reduced-motion: reduce` through the theme's
 * `motion.reducedMotion: 'system'`, which MUI's `Skeleton` honours.
 */
export function ProcessDefinitionSkeleton() {
  return (
    <Box role="status" aria-label="Загрузка данных">
      <Grid container spacing={2}>
        {Array.from({ length: SKELETON_CARDS }, (_, card) => (
          <Grid key={card} size={{ xs: 12, lg: 6 }}>
            <Paper sx={{ p: 2, height: '100%' }}>
              <Skeleton variant="text" sx={{ width: (theme) => theme.spacing(24) }} />
              <Skeleton variant="text" sx={{ width: (theme) => theme.spacing(36), mb: 2 }} />
              <Table size="small">
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
                  {Array.from({ length: SKELETON_ROWS }, (_, row) => (
                    <TableRow key={row}>
                      {STATE_COLUMNS.map((column) => (
                        <TableCell key={column}>
                          <Skeleton
                            variant="rounded"
                            sx={{ height: (theme) => theme.spacing(2.5) }}
                          />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Paper>
          </Grid>
        ))}
      </Grid>
    </Box>
  );
}
