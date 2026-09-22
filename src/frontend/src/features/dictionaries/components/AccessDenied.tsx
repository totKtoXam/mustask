import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';

/**
 * Экран "Доступ ограничен" — текст дословно из канона состояний экрана
 * (ADR-0007), переиспользуется во всех route-level гейтах этого среза. Не кнопка disabled без
 * объяснения — целая страница вместо контента.
 */
export function AccessDenied() {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center', py: 8 }}>
      <LockOutlinedIcon fontSize="large" color="disabled" sx={{ mb: 2 }} />
      <Typography variant="h6" component="h2" gutterBottom>
        Доступ ограничен
      </Typography>
      <Typography variant="body2" color="text.secondary">
        У вас нет прав для просмотра этого раздела. Обратитесь к администратору, чтобы запросить доступ.
      </Typography>
    </Box>
  );
}
