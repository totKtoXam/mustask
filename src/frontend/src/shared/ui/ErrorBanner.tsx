import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import { apiErrorText } from '../lib/apiErrorText';

/**
 * Баннер ошибки запроса: единый текст через apiErrorText.
 *
 * `onRetry` — необязателен: когда передан,
 * баннер получает действие "Повторить" (ADR-0007, состояние "ошибка с
 * повтором"); существующие вызовы без него продолжают работать без
 * изменений.
 */
export function ErrorBanner({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <Alert
      severity="error"
      sx={{ my: 2 }}
      action={
        onRetry && (
          <Button color="inherit" size="small" onClick={onRetry}>
            Повторить
          </Button>
        )
      }
    >
      {apiErrorText(error)}
    </Alert>
  );
}
