import Skeleton from '@mui/material/Skeleton';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Paper from '@mui/material/Paper';

interface RegistrySkeletonProps {
  /** Реальные заголовки колонок — структура таблицы видна сразу. */
  columns: string[];
  rows?: number;
}

/**
 * Скелетон реестра: 5 строк-заглушек внутри настоящего TableHead.
 *
 * Shared since mod-015 (design § 8.3): every registry loads the same way,
 * on the same outlined panel as its table, sized by the spacing unit.
 */
export function RegistrySkeleton({ columns, rows = 5 }: RegistrySkeletonProps) {
  return (
    <TableContainer component={Paper} variant="outlined" role="status" aria-label="Загрузка данных">
      <Table size="small">
        <TableHead>
          <TableRow>
            {columns.map((column) => (
              <TableCell key={column}>{column}</TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {Array.from({ length: rows }, (_, rowIndex) => (
            <TableRow key={rowIndex}>
              {columns.map((column, columnIndex) => (
                <TableCell key={`${rowIndex}-${columnIndex}-${column}`}>
                  <Skeleton variant="rounded" sx={{ height: (theme) => theme.spacing(3) }} />
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
