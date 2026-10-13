import type { ComponentType } from 'react';
import { ButtonsDemo } from './demos/ButtonsDemo';
import { CardDemo } from './demos/CardDemo';
import { ColoursDemo } from './demos/ColoursDemo';
import { DialogsDemo } from './demos/DialogsDemo';
import { FieldsDemo } from './demos/FieldsDemo';
import { IconsDemo } from './demos/IconsDemo';
import { MarksDemo } from './demos/MarksDemo';
import { MenusDemo } from './demos/MenusDemo';
import { NoticesDemo } from './demos/NoticesDemo';
import { PageHeadDemo } from './demos/PageHeadDemo';
import { SectionsDemo } from './demos/SectionsDemo';
import { SizesDemo } from './demos/SizesDemo';
import { StatesDemo } from './demos/StatesDemo';
import { TabsDemo } from './demos/TabsDemo';
import { TagsDemo } from './demos/TagsDemo';
import { TextDemo } from './demos/TextDemo';
import { TypographyDemo } from './demos/TypographyDemo';

/**
 * One section of the showcase (tech-015 design § 10.3). `covers` names the kit
 * components the section shows alive; the coverage test that reads it (KIT-19) moves to
 * tech-016 with the table family's section (status.md D-6), and a component added to the
 * kit lists itself here either way. The tokens' sections cover none.
 */
export interface ShowcaseSection {
  /** The section's anchor and its region's id; the contents link to `#id`. */
  id: string;
  /** The section's `h2` — final. */
  title: string;
  /** The sentence under the title — final. */
  usage: string;
  covers: readonly string[];
  Demo: ComponentType;
}

// The order of the page. The table family's section (design § 10.3, row 16) is not here:
// the showcase draws no table sections until tech-016 (status.md D-6).
export const showcaseRegistry: readonly ShowcaseSection[] = [
  {
    id: 'colours',
    title: 'Цвета',
    usage:
      'Все цвета панели берутся из темы. Цвет никогда не бывает единственным носителем смысла: рядом всегда текст или значок.',
    covers: [],
    Demo: ColoursDemo,
  },
  {
    id: 'typography',
    title: 'Шрифт и размеры текста',
    usage: 'Шрифт Segoe UI, два начертания: обычное и полужирное.',
    covers: [],
    Demo: TypographyDemo,
  },
  {
    id: 'sizes',
    title: 'Размеры и отступы',
    usage: 'Размеры берутся из темы или из шага сетки в 8 пикселей.',
    covers: [],
    Demo: SizesDemo,
  },
  {
    id: 'icons',
    title: 'Значки',
    usage:
      'Тонкие значки — для знаков, которые в образце нарисованы линией; остальные — контурные значки Material.',
    covers: [
      'ThinChevronDownIcon',
      'ThinChevronRightIcon',
      'ThinAddIcon',
      'ThinRemoveIcon',
      'ThinCloseIcon',
      'ThinExpandIcon',
      'ThinMoreVerticalIcon',
      'ThinRefreshIcon',
      'ThinUndoIcon',
      'ThinSaveIcon',
      'ThinPersonIcon',
      'BacklogIcon',
      'IconAction',
    ],
    Demo: IconsDemo,
  },
  {
    id: 'buttons',
    title: 'Кнопки',
    usage: 'На странице и в окне одна синяя кнопка; остальные серые или без заливки.',
    covers: ['ActionButton', 'SplitButton'],
    Demo: ButtonsDemo,
  },
  {
    id: 'menus',
    title: 'Меню',
    usage:
      'Меню открывается клавишей Enter, пробелом или стрелкой вниз; Esc закрывает его и возвращает фокус на кнопку.',
    covers: ['MenuButton', 'MoreActionsMenu'],
    Demo: MenusDemo,
  },
  {
    id: 'marks',
    title: 'Типы, состояния и люди',
    usage: 'Тип работы и состояние всегда подписаны текстом; цвет и значок только помогают.',
    covers: ['TypeMark', 'TypeLabel', 'CategoryMarker', 'StateLabel', 'UserAvatar', 'PersonLabel'],
    Demo: MarksDemo,
  },
  {
    id: 'tags',
    title: 'Теги',
    usage:
      'Тег — короткая метка работы; удалять и добавлять теги можно, только когда это разрешено.',
    covers: ['Tag', 'TagList'],
    Demo: TagsDemo,
  },
  {
    id: 'text',
    title: 'Длинный текст',
    usage: 'Длинный текст обрезается многоточием, а полный текст показывается подсказкой.',
    covers: ['TruncatedText', 'VisuallyHidden', 'LiveRegion', 'InlineEmpty'],
    Demo: TextDemo,
  },
  {
    id: 'page-head',
    title: 'Заголовок страницы',
    usage: 'Название страницы, команды и виды страницы; на странице он один.',
    covers: ['PageHeader'],
    Demo: PageHeadDemo,
  },
  {
    id: 'tabs',
    title: 'Вкладки карточки',
    usage:
      'Вкладки разделов карточки: значок, число и подпись. Одна вкладка — тоже допустимая полоса.',
    covers: ['BoxedTabs', 'BoxedTabPanel'],
    Demo: TabsDemo,
  },
  {
    id: 'fields',
    title: 'Поля ввода',
    usage:
      'Поле без рамки: подпись сверху, значение как текст, рамка только при наведении и фокусе.',
    covers: [
      'InlineTextField',
      'InlineTextArea',
      'InlineNumberField',
      'InlineDateField',
      'InlineSelectField',
      'InlinePickerField',
      'PersonField',
    ],
    Demo: FieldsDemo,
  },
  {
    id: 'sections',
    title: 'Разделы и колонки',
    usage:
      'Раздел — заголовок, линия и содержимое. Колонки перестраиваются по ширине самого блока, а не окна.',
    covers: ['Surface', 'Section', 'SectionHead', 'ColumnLayout'],
    Demo: SectionsDemo,
  },
  {
    id: 'card',
    title: 'Карточка',
    usage: 'Шапка, серая полоса свойств и тело карточки; на странице они в одной белой панели.',
    covers: ['CardHeaderBar', 'PropertyBand', 'TypeStripe', 'PanelShell'],
    Demo: CardDemo,
  },
  {
    id: 'dialogs',
    title: 'Окна',
    usage: 'Окно карточки, подтверждение и окно с формой. Занятое окно закрыть нельзя.',
    covers: ['DialogShell', 'ConfirmDialog', 'FormDialog'],
    Demo: DialogsDemo,
  },
  {
    id: 'notices',
    title: 'Уведомления',
    usage:
      'Короткое сообщение внизу слева исчезает через 6 секунд; ошибки, которые остаются на месте, показываются в самой странице.',
    covers: ['NoticeProvider', 'InlineNotice', 'ErrorBanner'],
    Demo: NoticesDemo,
  },
  {
    id: 'states',
    title: 'Пустые, ошибочные и загружаемые состояния',
    usage: 'Каждый экран одинаково показывает загрузку, пустоту, ошибку и отсутствие доступа.',
    covers: ['StateFrame', 'AccessDenied', 'NotFound', 'FieldsSkeleton', 'CardSkeleton'],
    Demo: StatesDemo,
  },
];
