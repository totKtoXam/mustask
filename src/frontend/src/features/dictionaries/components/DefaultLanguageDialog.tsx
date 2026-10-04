import { useMutation } from '@tanstack/react-query';
import DialogContentText from '@mui/material/DialogContentText';
import { apiErrorText } from '../../../shared/lib/apiErrorText';
import { ConfirmDialog } from '../../../shared/ui/ConfirmDialog';
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
 *
 * Since mod-015 the dialog is a thin consumer of the shared `ConfirmDialog`
 * (US-9, C-13): «Закрыть» in the title row, the refusal at the top of the
 * body, the same busy rules. Its copy, its mutation and its 409 text are
 * unchanged.
 */
export function DefaultLanguageDialog({
  language,
  onClose,
  onAssigned,
}: DefaultLanguageDialogProps) {
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
    <ConfirmDialog
      open={language !== null}
      title="Назначить языком по умолчанию"
      confirmLabel="Назначить"
      busy={mutation.isPending}
      error={mutation.isError ? apiErrorText(mutation.error) : null}
      onConfirm={() => mutation.mutate()}
      onClose={onClose}
    >
      <DialogContentText>
        Сделать «{language?.endonym}» ({language?.code}) языком по умолчанию?
      </DialogContentText>
      <DialogContentText sx={{ mt: 1 }}>
        Все активные записи должны быть переведены на этот язык — иначе изменение будет отклонено.
      </DialogContentText>
    </ConfirmDialog>
  );
}
