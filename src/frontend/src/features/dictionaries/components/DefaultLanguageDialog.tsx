import { useMutation } from '@tanstack/react-query';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { setDefaultLanguage, type LanguageResponse } from '../api/dictionariesApi';

export interface DefaultLanguageDialogTarget {
  code: string;
  endonym: string;
}

interface DefaultLanguageDialogProps {
  /** `null` — диалог закрыт; открывается только строкой реестра языков (StarBorderIcon). */
  language: DefaultLanguageDialogTarget | null;
  onClose: () => void;
  /** Успешный `200` — что показать (Snackbar/инвалидация) решает вызывающий (реестр языков), у самого диалога этого состояния нет. */
  onAssigned: (assigned: LanguageResponse) => void;
}

/**
 * Диалог «Назначить языком по умолчанию». Не маршрут: не
 * бывает открыт напрямую, поэтому не имеет собственного access-denied — в
 * момент показа триггер (строка реестра языков) уже гейтирован
 * `languages.write`. Запись `PUT /languages/default` не несёт `If-Match`
 * (исключение из общего правила контракта) — версия здесь не читается и не
 * отправляется вовсе, поэтому конфликт не показывается через
 * `VersionConflictPanel`: единственный отказ этого экрана — "не хватает
 * переводов" или "язык больше не активен" (оба — простой `Alert`).
 *
 * Рендерится вызывающим с `key={language?.code ?? 'closed'}`, чтобы каждое
 * новое открытие начинало мутацию с чистого состояния (загрузка/пусто здесь
 * неприменимы — единственное состояние "идёт сохранение" + ошибка, без
 * переноса ошибки одного открытия в следующее).
 */
export function DefaultLanguageDialog({ language, onClose, onAssigned }: DefaultLanguageDialogProps) {
  const mutation = useMutation({
    mutationFn: () => {
      if (!language) {
        return Promise.reject(new Error('DefaultLanguageDialog: нет цели для назначения.'));
      }
      return setDefaultLanguage(language.code);
    },
    onSuccess: (result) => onAssigned(result),
  });

  return (
    <Dialog open={language !== null} onClose={mutation.isPending ? undefined : onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Назначить языком по умолчанию</DialogTitle>
      <DialogContent>
        <DialogContentText>
          Сделать «{language?.endonym}» ({language?.code}) языком по умолчанию?
        </DialogContentText>
        <DialogContentText sx={{ mt: 1 }}>
          Все активные записи должны быть переведены на этот язык — иначе изменение будет отклонено.
        </DialogContentText>
        {mutation.isError && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {apiErrorText(mutation.error)}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        <Button
          variant="contained"
          onClick={() => mutation.mutate()}
          disabled={mutation.isPending}
          startIcon={mutation.isPending ? <CircularProgress size={16} /> : undefined}
        >
          Назначить
        </Button>
      </DialogActions>
    </Dialog>
  );
}
