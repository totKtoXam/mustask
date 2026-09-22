import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';

/** Центрированный индикатор загрузки для страниц и диалогов. */
export function LoadingBox() {
  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
      <CircularProgress aria-label="Загрузка" />
    </Box>
  );
}
