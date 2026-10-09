// The fields per type of feat-022 (spec § The field table, § Screens and copy;
// api-contract § 1): which type has which field, how each is drawn, its
// fixed list or its range, and where it stands on the form. A type is
// recognised by its appearance token (`type.appearance`), never by its name or
// key (api-contract § 0). The texts are the spec's.
import type {
  WorkItemFieldName,
  WorkItemTextFieldName,
  WorkItemValueFieldName,
} from '../api/workItemsApi';

/** api-contract § 1's 17 JSON names, in its order. */
export const WORK_ITEM_FIELD_NAMES: readonly WorkItemFieldName[] = [
  'priority',
  'risk',
  'storyPoints',
  'effort',
  'businessValue',
  'timeCriticality',
  'startDate',
  'targetDate',
  'severity',
  'activity',
  'originalEstimate',
  'remainingWork',
  'completedWork',
  'valueArea',
  'acceptanceCriteria',
  'reproSteps',
  'systemInfo',
];

/** The five kinds of type the process holds fixed (mod-001's appearance tokens). */
export const TYPE_APPEARANCES = ['epic', 'feature', 'story', 'task', 'bug'] as const;
export type TypeAppearance = (typeof TYPE_APPEARANCES)[number];

/** The fields drawn as a fixed list. */
export type ListFieldName = 'priority' | 'risk' | 'severity' | 'activity' | 'valueArea';

/** The fields drawn as a number. */
export type NumberFieldName =
  | 'storyPoints'
  | 'effort'
  | 'businessValue'
  | 'timeCriticality'
  | 'originalEstimate'
  | 'remainingWork'
  | 'completedWork';

/** The fields drawn as the browser's own date field (status.md D-6, OQ-2 (a)). */
export type DateFieldName = 'startDate' | 'targetDate';

/** The controls of the right column's sections: every field but the type texts. */
export type ControlFieldName = WorkItemValueFieldName;

/** One option of a fixed list: the key stored and sent, the label shown. */
export interface ListOption {
  key: string;
  label: string;
}

/** «Риск» and «Вид работ» offer this first: the empty value (BR-M3-31). */
export const NOT_SET_LABEL = 'Не указан';

export interface ListFieldSpec {
  label: string;
  /** In the spec's order. */
  options: readonly ListOption[];
  /** May be empty, offered as «Не указан»; an always-set list has no empty choice. */
  optional: boolean;
  /** The key is a JSON integer (`priority`, `risk`, `severity`), not a string. */
  numeric: boolean;
  helper?: string;
}

export const LIST_FIELDS: Readonly<Record<ListFieldName, ListFieldSpec>> = {
  priority: {
    label: 'Приоритет',
    options: [
      { key: '1', label: '1' },
      { key: '2', label: '2' },
      { key: '3', label: '3' },
      { key: '4', label: '4' },
    ],
    optional: false,
    numeric: true,
    helper: '1 — самый высокий',
  },
  risk: {
    label: 'Риск',
    options: [
      { key: '1', label: '1 — Высокий' },
      { key: '2', label: '2 — Средний' },
      { key: '3', label: '3 — Низкий' },
    ],
    optional: true,
    numeric: true,
  },
  severity: {
    label: 'Серьёзность',
    options: [
      { key: '1', label: '1 — Критическая' },
      { key: '2', label: '2 — Высокая' },
      { key: '3', label: '3 — Средняя' },
      { key: '4', label: '4 — Низкая' },
    ],
    optional: false,
    numeric: true,
  },
  activity: {
    label: 'Вид работ',
    options: [
      { key: 'Deployment', label: 'Развёртывание' },
      { key: 'Design', label: 'Проектирование' },
      { key: 'Development', label: 'Разработка' },
      { key: 'Documentation', label: 'Документирование' },
      { key: 'Requirements', label: 'Требования' },
      { key: 'Testing', label: 'Тестирование' },
    ],
    optional: true,
    numeric: false,
  },
  valueArea: {
    label: 'Вид ценности',
    options: [
      { key: 'Business', label: 'Бизнес' },
      { key: 'Architectural', label: 'Архитектура' },
    ],
    optional: false,
    numeric: false,
  },
};

/** An empty estimate's placeholder: "not estimated", never 0 (BR-M3-32). */
const NOT_ESTIMATED = 'Не оценено';
/** An empty relative number's placeholder. */
const NOT_GIVEN = 'Не задано';
const DECIMAL_HELPER = 'От 0 до 9 999';

export interface NumberFieldSpec {
  label: string;
  /** «Бизнес-ценность»: an integer; every other number holds two decimals. */
  integer: boolean;
  placeholder: string;
  helper: string;
}

export const NUMBER_FIELDS: Readonly<Record<NumberFieldName, NumberFieldSpec>> = {
  storyPoints: {
    label: 'Оценка в пунктах',
    integer: false,
    placeholder: NOT_ESTIMATED,
    helper: DECIMAL_HELPER,
  },
  effort: {
    label: 'Трудоёмкость',
    integer: false,
    placeholder: NOT_ESTIMATED,
    helper: DECIMAL_HELPER,
  },
  businessValue: {
    label: 'Бизнес-ценность',
    integer: true,
    placeholder: NOT_GIVEN,
    helper: 'Целое число от 0 до 9 999',
  },
  timeCriticality: {
    label: 'Критичность по времени',
    integer: false,
    placeholder: NOT_GIVEN,
    helper: DECIMAL_HELPER,
  },
  originalEstimate: {
    label: 'Исходная оценка',
    integer: false,
    placeholder: NOT_ESTIMATED,
    helper: DECIMAL_HELPER,
  },
  remainingWork: {
    label: 'Оставшаяся работа',
    integer: false,
    placeholder: NOT_ESTIMATED,
    helper: DECIMAL_HELPER,
  },
  completedWork: {
    label: 'Выполненная работа',
    integer: false,
    placeholder: NOT_ESTIMATED,
    helper: DECIMAL_HELPER,
  },
};

export interface DateFieldSpec {
  label: string;
  helper?: string;
}

export const DATE_FIELDS: Readonly<Record<DateFieldName, DateFieldSpec>> = {
  startDate: { label: 'Дата начала' },
  targetDate: { label: 'Целевая дата', helper: 'Не раньше даты начала' },
};

export interface TextFieldSpec {
  label: string;
  placeholder: string;
}

/** The type texts: each an `h2` section of its own in the left column, under «Описание». */
export const TEXT_FIELDS: Readonly<Record<WorkItemTextFieldName, TextFieldSpec>> = {
  acceptanceCriteria: {
    label: 'Критерии приёмки',
    placeholder: 'Опишите, по каким признакам история считается готовой',
  },
  reproSteps: {
    label: 'Шаги воспроизведения',
    placeholder: 'Опишите шаги, ожидаемый и фактический результат',
  },
  systemInfo: {
    label: 'Сведения о системе',
    placeholder: 'Опишите окружение: ОС, браузер, версию приложения',
  },
};

/** The type texts' helper, as «Описание»'s. */
export const TEXT_HELPER = 'До 100 000 символов';

/** The right column's section headings (spec § The form's sections). */
export const SECTION_TITLES = {
  planning: 'Планирование',
  estimation: 'Трудозатраты (часы)',
  classification: 'Классификация',
} as const;

export interface FieldSection {
  title: (typeof SECTION_TITLES)[keyof typeof SECTION_TITLES];
  fields: readonly ControlFieldName[];
}

/** Where a type's fields stand: its texts under «Описание», its sections in the right column. */
export interface FormLayout {
  texts: readonly WorkItemTextFieldName[];
  sections: readonly FieldSection[];
}

const PORTFOLIO_LAYOUT: FormLayout = {
  texts: [],
  sections: [
    {
      title: SECTION_TITLES.planning,
      fields: [
        'priority',
        'risk',
        'effort',
        'businessValue',
        'timeCriticality',
        'startDate',
        'targetDate',
      ],
    },
    { title: SECTION_TITLES.classification, fields: ['valueArea'] },
  ],
};

const ESTIMATION: FieldSection = {
  title: SECTION_TITLES.estimation,
  fields: ['originalEstimate', 'remainingWork', 'completedWork'],
};

/** spec § The form's sections, per type, top to bottom. */
const LAYOUTS: Readonly<Record<TypeAppearance, FormLayout>> = {
  epic: PORTFOLIO_LAYOUT,
  feature: PORTFOLIO_LAYOUT,
  story: {
    texts: ['acceptanceCriteria'],
    sections: [
      { title: SECTION_TITLES.planning, fields: ['storyPoints', 'priority', 'risk'] },
      { title: SECTION_TITLES.classification, fields: ['valueArea'] },
    ],
  },
  task: {
    texts: [],
    sections: [{ title: SECTION_TITLES.planning, fields: ['priority', 'activity'] }, ESTIMATION],
  },
  bug: {
    texts: ['reproSteps', 'systemInfo'],
    sections: [
      {
        title: SECTION_TITLES.planning,
        fields: ['storyPoints', 'priority', 'severity', 'activity'],
      },
      ESTIMATION,
    ],
  },
};

/** No section and no field: a kind the process does not hold. */
const NO_FIELDS: FormLayout = { texts: [], sections: [] };

function isTypeAppearance(appearance: string): appearance is TypeAppearance {
  return (TYPE_APPEARANCES as readonly string[]).includes(appearance);
}

/** The type's texts and sections; a kind the process does not hold shows none. */
export function layoutOf(appearance: string): FormLayout {
  return isTypeAppearance(appearance) ? LAYOUTS[appearance] : NO_FIELDS;
}

/** The fields the type has (BR-M3-30): its texts, then its sections' controls, in form order. */
export function fieldsOf(appearance: string): readonly WorkItemFieldName[] {
  const layout = layoutOf(appearance);
  return [...layout.texts, ...layout.sections.flatMap((section) => section.fields)];
}

/** How a field is drawn and checked. */
export type FieldKind = 'list' | 'number' | 'date' | 'text';

export function kindOf(name: WorkItemFieldName): FieldKind {
  if (name in LIST_FIELDS) return 'list';
  if (name in NUMBER_FIELDS) return 'number';
  if (name in DATE_FIELDS) return 'date';
  return 'text';
}

export function isListField(name: WorkItemFieldName): name is ListFieldName {
  return kindOf(name) === 'list';
}

export function isNumberField(name: WorkItemFieldName): name is NumberFieldName {
  return kindOf(name) === 'number';
}

export function isDateField(name: WorkItemFieldName): name is DateFieldName {
  return kindOf(name) === 'date';
}

export function isTextField(name: WorkItemFieldName): name is WorkItemTextFieldName {
  return kindOf(name) === 'text';
}
