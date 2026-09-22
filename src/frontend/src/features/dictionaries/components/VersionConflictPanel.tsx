import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Stack from '@mui/material/Stack';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import { formatDateTime } from '../../../shared/lib/format';
import type { ConflictField } from '../hooks/useConflictRecovery';

interface VersionConflictPanelProps {
  status: 'checking' | 'comparing';
  fields: ConflictField[];
  /**
   * `updatedAt`/`updatedBy` из `null` — черновик ещё не сохранён этим блоком.
   * Оба штампа — сырой ISO с сервера; форматирует их сама
   * панель, чтобы у мест вызова не осталось шансов забыть отформатировать (или отформатировать по-разному) одну и
   * ту же дату.
   */
  mine: { updatedAt: string | null; updatedBy: string | null };
  server: { updatedAt: string; updatedBy: string };
  onKeepMine: () => void;
  onAcceptServer: () => void;
}

/**
 * Панель версионного конфликта: при конфликте оба текста сохраняются, и
 * администратор сливает их вручную, а не теряет ни одну из сторон. Один
 * компонент, встраивается ровно в то место экрана, где случился конфликт
 * (сегодня — форма языка); соседние блоки не трогает — это решает сам
 * вызывающий, монтируя панель локально.
 */
export function VersionConflictPanel({
  status,
  fields,
  mine,
  server,
  onKeepMine,
  onAcceptServer,
}: VersionConflictPanelProps) {
  if (status === 'checking') {
    return (
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', py: 1 }}>
        <CircularProgress size={16} />
        <Typography variant="body2" color="text.secondary">
          Проверяем текущую версию…
        </Typography>
      </Stack>
    );
  }

  return (
    <Box>
      <Alert severity="warning" sx={{ mb: 2 }}>
        Запись уже изменена другим пользователем. Сравните значения и выберите, что сохранить.
      </Alert>

      <Table size="small" sx={{ mb: 2 }}>
        <TableHead>
          <TableRow>
            <TableCell />
            <TableCell>Ваш вариант</TableCell>
            <TableCell>Текущая версия на сервере</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {fields.map((field) => {
            const changed = field.mine !== field.server;
            return (
              <TableRow key={field.label}>
                <TableCell component="th" scope="row">
                  {field.label}
                </TableCell>
                <TableCell>{field.mine}</TableCell>
                <TableCell>
                  <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                    <span>{field.server}</span>
                    {changed && <Chip label="изменилось" color="warning" size="small" />}
                  </Stack>
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>

      <Stack direction="row" spacing={4} sx={{ mb: 2 }}>
        <Typography variant="caption" color="text.secondary">
          Обновлено: {mine.updatedAt ? `${formatDateTime(mine.updatedAt)} — ${mine.updatedBy}` : '— (ваш черновик)'}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          Обновлено: {formatDateTime(server.updatedAt)} — {server.updatedBy}
        </Typography>
      </Stack>

      <Stack direction="row" spacing={2} sx={{ justifyContent: 'flex-end' }}>
        <Button onClick={onAcceptServer}>Принять серверный вариант</Button>
        <Button variant="contained" onClick={onKeepMine}>
          Сохранить свой вариант
        </Button>
      </Stack>
    </Box>
  );
}
