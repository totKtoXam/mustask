/**
 * The editor's interface texts (feat-018 design § Interface texts, plan § 9).
 * Russian defaults, as `FormDialog` carries «Отмена»; the caller may override
 * any of them through the `copy` prop (design: "Limits and copy are props").
 * Every string is the stories' or the design's — nothing is made up here.
 */
export interface RichTextCopy {
  /** The toolbar's accessible name. */
  toolbarLabel: string;
  /** Shown while the body is empty (also `aria-placeholder`). */
  placeholder: string;
  /** Hidden text while the editor's code loads. */
  loading: string;
  /** The chunk failed to load; the button repeats the load. */
  loadFailed: string;
  retry: string;
  /** Live over the visible limit: «Не более 100 000 символов.». */
  tooLong: string;
  /** The counter, «98 450 / 100 000». */
  counter: (count: number, limit: number) => string;
  /** The counter's accessible name, «Символов: 98 450 из 100 000». */
  counterName: (count: number, limit: number) => string;
  /** Control names: the accessible name, the tooltip's first part. */
  controls: Record<
    'undo' | 'redo' | 'h3' | 'h4' | 'bold' | 'italic' | 'bullet' | 'ordered' | 'indent' | 'outdent',
    string
  >;
}

const numbers = new Intl.NumberFormat('ru-RU');

export const DEFAULT_COPY: RichTextCopy = {
  toolbarLabel: 'Форматирование описания',
  placeholder: 'Добавьте описание',
  loading: 'Загрузка редактора',
  loadFailed: 'Не удалось загрузить редактор.',
  retry: 'Повторить',
  tooLong: 'Не более 100 000 символов.',
  counter: (count, limit) => `${numbers.format(count)} / ${numbers.format(limit)}`,
  counterName: (count, limit) => `Символов: ${numbers.format(count)} из ${numbers.format(limit)}`,
  controls: {
    undo: 'Отменить',
    redo: 'Повторить',
    h3: 'Заголовок 3',
    h4: 'Заголовок 4',
    bold: 'Полужирный',
    italic: 'Курсив',
    bullet: 'Маркированный список',
    ordered: 'Нумерованный список',
    indent: 'Увеличить отступ списка',
    outdent: 'Уменьшить отступ списка',
  },
};
