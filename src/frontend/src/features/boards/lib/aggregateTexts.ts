// The interface copy of the aggregate views, final in design.md § 3, § 5, § 6 (Russian, by the
// language policy). A text is never made up here.
export const aggregateTexts = {
  loadingGroup: 'Загрузка элементов группы',
  loadingStream: 'Загрузка элементов',
  loadingHeaders: 'Загрузка данных',
  retrying: 'Сервис занят, пробуем ещё раз…',
  refusedGroup: 'Не удалось загрузить элементы группы. Сервис занят, попробуйте ещё раз.',
  refusedStream: 'Не удалось загрузить элементы. Сервис занят, попробуйте ещё раз.',
  errorGroup: 'Не удалось загрузить элементы группы.',
  errorStream: 'Не удалось загрузить элементы.',
  emptyGroup: 'В группе больше нет элементов этого уровня',
  emptyStream: 'Элементов этого уровня больше нет',
  emptyBacklog: 'В бэклоге пока нет элементов',
  emptyBoard: 'На доске пока нет элементов',
  emptyList: 'Рабочих элементов пока нет',
  nothingFound: 'Ничего не найдено',
  showMore: 'Показать ещё',
  retry: 'Повторить',
  collapseAll: 'Свернуть все',
  projectGone: 'Проект больше недоступен: группа скрыта.',
  noTeam: 'Без команды',
  noDepartment: 'Без департамента',
  moveHeading: 'Переместить',
  moveToColumn: 'Переместить в колонку',
  moveActions: 'Ещё действия',
  loadMoreFirst: 'Сначала нажмите «Показать ещё»',
  movedToEnd: 'Элемент перенесён в конец бэклога проекта. Он появится после «Показать ещё».',
} as const;

export function loadedMore(count: number): string {
  return `Загружено ещё элементов: ${count}`;
}

/** «40», or «3 из 40» when a selector or a filter narrows. */
export function countText(count: number, total: number): string {
  return count === total ? String(count) : `${count} из ${total}`;
}

/** The stream's count line in the page bar. */
export function streamCountText(count: number, total: number): string {
  return `Элементов: ${countText(count, total)}`;
}
