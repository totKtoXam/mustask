// The work item card's interface texts, final in feat-029's design § 9 and
// nowhere else. The labels, lists, hints and placeholders of the fields stay
// in `fields.ts`; the kit owns its few defaults («Развернуть на весь экран»,
// «Закрыть», «Ещё действия», «Загрузка данных», «Продолжить редактирование»).

export const cardTexts = {
  title: { label: 'Заголовок', placeholder: 'Введите заголовок' },
  description: { label: 'Описание', placeholder: 'Добавьте описание' },
  assignee: { label: 'Исполнитель' },
  state: { label: 'Статус' },
  area: { label: 'Область' },
  parent: { label: 'Родитель' },
  /** The Save group: its name, the main button, the arrow and the one variant. */
  save: {
    group: 'Сохранение',
    label: 'Сохранить',
    menu: 'Ещё варианты сохранения',
    andClose: 'Сохранить и закрыть',
  },
  refresh: 'Обновить',
  undo: 'Отменить все изменения',
  /** «⋮» is the kit's `MoreActionsMenu`, named «Ещё действия» by default. */
  copyLink: 'Копировать ссылку',
  tabs: { label: 'Разделы рабочего элемента', details: 'Детали' },
  relatedWork: 'Связанная работа',
  children: 'Дочерние элементы',
  noChildren: 'Дочерних элементов нет.',
  readOnly: 'Рабочий элемент доступен только для чтения.',
  notices: {
    saved: 'Изменения сохранены.',
    refreshed: 'Данные обновлены.',
    refreshedKept: 'Данные обновлены. Несохранённые изменения остались на месте.',
    undone: 'Изменения отменены.',
    linkCopied: 'Ссылка скопирована.',
    linkCopyFailed: 'Не удалось скопировать ссылку.',
  },
  /** The title of the error banner and of the loading card's page heading. */
  frame: { errorTitle: 'Рабочий элемент', notFoundHint: 'Проверьте адрес или закройте окно.' },
} as const;

/** «Изменён: <дата>» — the band's stamp; the date is `formatDateTime`'s. */
export function changedAtText(formatted: string): string {
  return `Изменён: ${formatted}`;
}

/** The dialog's name while the card loads or fails and has no heading yet. */
export function loadingName(identifier: string): string {
  return `Рабочий элемент ${identifier}`;
}

/** A confirmation over the card: what it asks and what its two buttons say. */
export interface GuardTexts {
  title: string;
  text: string;
  cancelLabel: string;
  confirmLabel: string;
}

const KEEP_EDITING = 'Продолжить редактирование';

function lostText(identifier: string): string {
  return `Несохранённые изменения рабочего элемента ${identifier} будут потеряны.`;
}

/** Closing the dialog with edits (D-4, Q-1): the dialog's ×, Esc and a click outside. */
export function closeGuardTexts(identifier: string): GuardTexts {
  return {
    title: 'Закрыть без сохранения?',
    text: lostText(identifier),
    cancelLabel: KEEP_EDITING,
    confirmLabel: 'Закрыть без сохранения',
  };
}

/** Switching to a related item with edits: a switch is not a close, so it has its own words. */
export function switchGuardTexts(identifier: string): GuardTexts {
  return {
    title: 'Перейти без сохранения?',
    text: lostText(identifier),
    cancelLabel: KEEP_EDITING,
    confirmLabel: 'Перейти без сохранения',
  };
}

/** «Отменить все изменения». */
export function undoGuardTexts(identifier: string): GuardTexts {
  return {
    title: 'Отменить все изменения?',
    text: `Поля вернутся к сохранённым значениям. ${lostText(identifier)}`,
    cancelLabel: KEEP_EDITING,
    confirmLabel: 'Отменить изменения',
  };
}
