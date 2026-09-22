import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';

/**
 * Стартовая страница закрытой части. Пока разделов с задачами нет, это
 * нейтральное приветствие; сводка появится вместе с первыми рабочими
 * разделами.
 */
export function HomePage() {
  return (
    <Box>
      <Typography variant="h5" component="h2" sx={{ mb: 2 }}>
        Главная
      </Typography>
      <Paper sx={{ p: 3 }}>
        <Stack spacing={1}>
          <Typography variant="h6" component="p">
            Добро пожаловать в Mustask
          </Typography>
          <Typography variant="body1" color="text.secondary">
            Mustask — трекер задач. Разделы появятся в меню слева по мере их готовности.
          </Typography>
        </Stack>
      </Paper>
    </Box>
  );
}
