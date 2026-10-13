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
    | 'undo'
    | 'redo'
    | 'h3'
    | 'h4'
    | 'size'
    | 'bold'
    | 'italic'
    | 'underline'
    | 'strike'
    | 'code'
    | 'colour'
    | 'highlight'
    | 'clear'
    | 'bullet'
    | 'ordered'
    | 'indent'
    | 'outdent'
    | 'link'
    | 'unlink'
    | 'open'
    | 'emoji'
    | 'codeblock'
    | 'language'
    | 'copy'
    | 'table'
    | 'tableOps',
    string
  >;
  /** The palettes: the label of each token by its vocabulary name, and the reset item. */
  colours: Record<string, string>;
  resetColour: string;
  highlights: Record<string, string>;
  resetHighlight: string;
  sizes: Record<string, string>;
  resetSize: string;
  /** «Простой текст» and the closed list of languages, by vocabulary name; `plain` is the first. */
  languages: Record<string, string>;
  /** The table menu's items. */
  tableMenu: Record<
    | 'rowAbove'
    | 'rowBelow'
    | 'columnLeft'
    | 'columnRight'
    | 'deleteRow'
    | 'deleteColumn'
    | 'headerRow'
    | 'deleteTable',
    string
  >;
  link: {
    title: string;
    address: string;
    addressHint: string;
    text: string;
    apply: string;
    addressRequired: string;
    addressRefused: string;
  };
  emoji: { title: string; search: string; empty: string };
  /** The notice line and its announcements. */
  notices: {
    image: string;
    codeHint: string;
    tableHint: string;
    codeCopied: string;
  };
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
    size: 'Размер шрифта',
    bold: 'Полужирный',
    italic: 'Курсив',
    underline: 'Подчёркнутый',
    strike: 'Зачёркнутый',
    code: 'Код в строке',
    colour: 'Цвет текста',
    highlight: 'Выделение цветом',
    clear: 'Очистить форматирование',
    bullet: 'Маркированный список',
    ordered: 'Нумерованный список',
    indent: 'Увеличить отступ списка',
    outdent: 'Уменьшить отступ списка',
    link: 'Ссылка',
    unlink: 'Убрать ссылку',
    open: 'Открыть в новой вкладке',
    emoji: 'Эмодзи',
    codeblock: 'Блок кода',
    language: 'Язык кода',
    copy: 'Копировать код',
    table: 'Таблица',
    tableOps: 'Операции с таблицей',
  },
  colours: {
    gray: 'Серый',
    red: 'Красный',
    orange: 'Оранжевый',
    green: 'Зелёный',
    teal: 'Бирюзовый',
    blue: 'Синий',
    purple: 'Фиолетовый',
    pink: 'Розовый',
  },
  resetColour: 'Обычный цвет',
  highlights: {
    yellow: 'Жёлтый',
    orange: 'Оранжевый',
    red: 'Красный',
    green: 'Зелёный',
    teal: 'Бирюзовый',
    blue: 'Синий',
    purple: 'Фиолетовый',
    gray: 'Серый',
  },
  resetHighlight: 'Без выделения',
  sizes: { xs: 'Очень мелкий', sm: 'Мелкий', lg: 'Крупный', xl: 'Очень крупный' },
  resetSize: 'Обычный размер',
  languages: {
    plain: 'Простой текст',
    javascript: 'JavaScript',
    typescript: 'TypeScript',
    json: 'JSON',
    html: 'HTML',
    xml: 'XML',
    css: 'CSS',
    sql: 'SQL',
    csharp: 'C#',
    python: 'Python',
    bash: 'Bash',
    yaml: 'YAML',
    markdown: 'Markdown',
  },
  tableMenu: {
    rowAbove: 'Добавить строку выше',
    rowBelow: 'Добавить строку ниже',
    columnLeft: 'Добавить столбец слева',
    columnRight: 'Добавить столбец справа',
    deleteRow: 'Удалить строку',
    deleteColumn: 'Удалить столбец',
    headerRow: 'Строка заголовка',
    deleteTable: 'Удалить таблицу',
  },
  link: {
    title: 'Ссылка',
    address: 'Адрес ссылки',
    addressHint: 'https://…',
    text: 'Текст ссылки',
    apply: 'Применить',
    addressRequired: 'Укажите адрес ссылки.',
    addressRefused: 'Допустимы адреса, начинающиеся с http://, https://, mailto: или www.',
  },
  emoji: { title: 'Эмодзи', search: 'Поиск эмодзи', empty: 'Ничего не найдено' },
  notices: {
    image: 'Изображения в описание не вставляются.',
    codeHint: 'Tab вставляет отступ. Esc, затем Tab — выйти из блока.',
    tableHint: 'Tab переходит между ячейками. Esc, затем Tab — выйти из таблицы.',
    codeCopied: 'Код скопирован',
  },
};
