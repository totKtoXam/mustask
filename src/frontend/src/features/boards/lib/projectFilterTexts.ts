/** The texts of the project filter of a team's views, final in feat-040 design § 13 and nowhere else. */
export const projectFilterTexts = {
  label: 'Проект',
  allProjects: 'Все проекты',
  /** A value in the address that names no project the team works on: the filter is on, and says no more. */
  filterOn: 'Фильтр задан',
  none: 'Нет проектов',
  loading: 'Загрузка…',
  failed: 'Не удалось загрузить проекты. Попробуйте ещё раз.',
} as const;
