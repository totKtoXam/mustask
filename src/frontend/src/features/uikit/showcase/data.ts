// The showcase's demo data (tech-015 design § 10.4): Russian, fixed, local. The page
// makes no API request; every demo is fed from here.

export const DEMO_PROJECT_KEY = 'MSK';
export const DEMO_IDENTIFIER = `${DEMO_PROJECT_KEY}-US-2`;
export const DEMO_TYPE_LABEL = 'Пользовательская история';
export const DEMO_TITLE = 'Письмо со ссылкой';

/** A type's stored appearance token and its name. */
export const DEMO_TYPES = [
  { appearance: 'epic', name: 'Эпик' },
  { appearance: 'feature', name: 'Фича' },
  { appearance: 'story', name: 'Пользовательская история' },
  { appearance: 'bug', name: 'Баг' },
  { appearance: 'task', name: 'Задача' },
] as const;

/** A state's category code and its name. */
export const DEMO_STATES = [
  { code: 'Proposed', name: 'Новая' },
  { code: 'InProgress', name: 'В работе' },
  { code: 'Resolved', name: 'Решена' },
  { code: 'Completed', name: 'Завершена' },
  { code: 'Removed', name: 'Удалена' },
] as const;

export const DEMO_PEOPLE = ['Анна Смирнова', 'Борис Ли'] as const;

export const DEMO_AREAS = ['Платформа', 'Платформа / Вход', 'Платформа / Отчёты'] as const;

export const DEMO_PRIORITIES = ['1', '2', '3', '4'] as const;

export const DEMO_PROJECTS = ['Alpha', 'Beta', 'Gamma', 'Delta'] as const;

export const DEMO_DESCRIPTION = 'Пользователь получает письмо со ссылкой для входа.';

export const DEMO_LONG_TITLE =
  'Описать end-to-end сценарии использования текущей системы и вынести неизвестные правила отдельным списком вопросов';

export const DEMO_TAGS = ['Discovery', 'Релиз 2', 'Риск', 'Сеть'] as const;
