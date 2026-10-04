import Alert from '@mui/material/Alert';
import AlertTitle from '@mui/material/AlertTitle';
import Button from '@mui/material/Button';
import { apiErrorText } from '../lib/apiErrorText';

/**
 * Баннер ошибки запроса: единый текст через apiErrorText.
 *
 * `onRetry` — необязателен: когда передан,
 * баннер получает действие "Повторить" (ADR-0007, состояние "ошибка с
 * повтором"); существующие вызовы без него продолжают работать без
 * изменений.
 *
 * `title` is optional too (mod-015 design § 7.1): a page may name what
 * failed above the server's text, e.g. «Не удалось загрузить процесс».
 * Without it the banner reads as it always has.
 */
export function ErrorBanner({
  error,
  onRetry,
  title,
}: {
  error: unknown;
  onRetry?: () => void;
  title?: string;
}) {
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
      {title && <AlertTitle>{title}</AlertTitle>}
      {apiErrorText(error)}
    </Alert>
  );
}
