import Paper from '@mui/material/Paper';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { PageHeader } from '../../shared/ui/PageHeader';

/**
 * The start page of the signed-in panel. While there are no work sections
 * yet, it is a neutral greeting; a summary arrives with the first work
 * sections. The page header and the panel are the shared patterns (mod-015
 * design § 11.1).
 */
export function HomePage() {
  return (
    <>
      <PageHeader title="Главная" />
      <Paper variant="outlined" sx={{ p: 3 }}>
        <Stack spacing={1}>
          <Typography variant="h2" component="p">
            Добро пожаловать в Mustask
          </Typography>
          <Typography variant="body1" color="text.secondary">
            Mustask — трекер задач. Разделы появятся в меню слева по мере их готовности.
          </Typography>
        </Stack>
      </Paper>
    </>
  );
}
