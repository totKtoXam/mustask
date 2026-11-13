// Every interface text of the slice, in one place, from docs/features/mod-007-iterations/design.md
// (§ 3–§ 7, § 10) and the stories. Refusals are never typed here: their text is the server's.

/** Russian plural: `one`, `few` (2-4), `many` (0, 5-20, …). */
export function plural(n: number, one: string, few: string, many: string): string {
  const mod100 = n % 100;
  const mod10 = n % 10;
  if (mod100 >= 11 && mod100 <= 14) return many;
  if (mod10 === 1) return one;
  if (mod10 >= 2 && mod10 <= 4) return few;
  return many;
}

export const itemsCount = (n: number) => `${n} ${plural(n, 'элемент', 'элемента', 'элементов')}`;
export const sprintsCount = (n: number) => `${n} ${plural(n, 'спринт', 'спринта', 'спринтов')}`;

export const sprintTexts = {
  sectionLabel: 'Спринты',
  teamNotFound: 'Команда не найдена',
  list: {
    tableLabel: 'Спринты команды',
    create: 'Создать спринт',
    columns: {
      number: '№',
      name: 'Название',
      state: 'Состояние',
      start: 'Начало',
      end: 'Окончание',
      items: 'Элементы',
      storyPoints: 'Story points',
    },
    states: { Planned: 'Запланирован', Active: 'Активен', Closed: 'Завершён' },
    rowMenu: (name: string) => `Действия со спринтом ${name}`,
    loadFailed: 'Не удалось загрузить спринты',
    moreFailed: 'Не удалось загрузить ещё спринты.',
    retry: 'Повторить',
    more: 'Показать ещё',
    forbidden: 'Недостаточно прав',
    empty: {
      heading: 'У команды пока нет спринтов',
      plannerHint: 'Создайте первый спринт, чтобы запланировать работу команды.',
      otherHint: 'Спринты создают участники и менеджеры команды.',
    },
    closedOf: (closed: number, held: number) => `${closed} из ${held}`,
  },
  commands: {
    edit: 'Изменить',
    start: 'Начать спринт',
    remove: 'Удалить',
    close: 'Завершить спринт',
    rename: 'Переименовать',
  },
  notices: {
    created: 'Спринт создан.',
    updated: 'Спринт обновлён.',
    renamed: 'Спринт переименован.',
    started: 'Спринт начат.',
    closed: 'Спринт завершён.',
    removed: 'Спринт удалён.',
  },
  form: {
    createTitle: 'Новый спринт',
    editTitle: 'Изменить спринт',
    renameTitle: 'Переименовать спринт',
    name: 'Название',
    nameHint: (number: number) => `Если оставить пустым, название будет «Спринт ${number}»`,
    nameHintCreate: 'Если оставить пустым, название будет «Спринт <номер>»',
    startDate: 'Дата начала',
    endDate: 'Дата окончания',
    activeDatesHint: 'Даты активного спринта можно менять, но не очищать.',
    create: 'Создать',
    save: 'Сохранить',
    nameInvalid: 'Название спринта — от 1 до 100 символов.',
    datesUnpaired: 'Укажите обе даты спринта или ни одной.',
    datesReversed: 'Дата окончания не может быть раньше даты начала.',
    activeDatesRequired: 'У активного спринта должны быть обе даты.',
  },
  start: {
    title: 'Начать спринт',
    text: (name: string) =>
      `Спринт «${name}» станет активным. Его даты можно будет менять, но не очищать, а вернуть спринт в «Запланирован» нельзя.`,
    confirm: 'Начать',
    noDates: 'Чтобы начать спринт, укажите обе даты.',
    setDates: 'Указать даты',
  },
  remove: {
    title: 'Удалить спринт',
    text: (name: string) =>
      `Спринт «${name}» будет удалён. Его номер не используется повторно, а освободившееся название можно занять снова.`,
    confirm: 'Удалить',
  },
  close: {
    title: (name: string) => `Завершить спринт «${name}»`,
    moving: (items: number) => `Незавершённые элементы: ${items} — их нужно перенести.`,
    nothingToMove: 'Незавершённых элементов нет.',
    staying: (items: number, points: number) =>
      `Останутся в спринте и войдут в итог: ${itemsCount(items)}, ${points} SP.`,
    destination: 'Куда перенести незавершённые элементы',
    destinationEmpty: 'Выберите спринт или «Без спринта»',
    noSprint: 'Без спринта',
    irreversible: 'Завершение нельзя отменить: спринт не вернуть в работу, а итог не изменится.',
    submit: 'Завершить спринт',
    busy: 'Завершаем…',
    optionsFailed: 'Не удалось загрузить спринты.',
    retry: 'Повторить',
  },
  outcome: {
    notFound: 'Спринт не найден',
    dates: 'Даты',
    closedBy: 'Завершил',
    level: 'Уровень',
    levels: { epics: 'Эпики', features: 'Фичи', stories: 'Истории' },
    from: 'С',
    to: 'По',
    apply: 'Применить',
    datesReversed: 'Дата окончания не может быть раньше даты начала.',
    inRange: (n: number) => `В диапазоне: ${sprintsCount(n)}`,
    capped: (shown: number, total: number) =>
      `Показаны последние ${shown} из ${total} спринтов диапазона.`,
    retry: 'Повторить',
    more: 'Показать ещё',
    dynamics: {
      title: 'Динамика',
      tableLabel: 'Динамика спринта',
      failed: 'Не удалось загрузить динамику спринта',
      emptyRange: 'В выбранном диапазоне нет завершённых спринтов.',
      this: '(этот)',
      columns: {
        sprint: 'Спринт',
        end: 'Окончание',
        items: 'Элементов',
        storyPoints: 'SP',
        closedItems: 'Закрыто элементов',
        closedItemsPercent: '% элементов',
        closedStoryPoints: 'Закрыто SP',
        closedStoryPointsPercent: '% SP',
      },
      summary: { min: 'Мин', average: 'Среднее', max: 'Макс' },
    },
    participants: {
      title: 'Итоги по участникам',
      tableLabel: 'Итоги по участникам',
      failed: 'Не удалось загрузить итоги по участникам',
      empty: 'На выбранном уровне в спринте не было элементов.',
      columns: {
        person: 'Участник',
        items: 'Элементов',
        storyPoints: 'SP',
        closedStoryPoints: 'Закрыто SP',
        closedStoryPointsPercent: '% SP',
        closedItems: 'Закрыто элементов',
        closedItemsPercent: '% элементов',
        compared: 'Сравнение',
      },
      noAssignee: 'Без исполнителя',
      total: 'Итого',
      notInOrganization: 'не в организации',
      unknownUser: 'Неизвестный пользователь',
      noData: 'Нет данных для сравнения',
      comparedOf: (k: number, n: number) => `${k} из ${n}`,
      expand: (name: string) => `Динамика: ${name}`,
      nestedLabel: (name: string) => `Динамика: ${name}`,
      caption: (k: number, n: number) => `По ${k} из ${n} спринтов`,
    },
    result: {
      title: 'Итог спринта',
      tableLabel: 'Итог спринта',
      failed: 'Не удалось загрузить итог спринта',
      empty: 'В итоге нет выполненных элементов.',
      count: (items: number, points: number) => `${itemsCount(items)}, ${points} SP`,
      columns: {
        identifier: 'Идентификатор',
        title: 'Название',
        project: 'Проект',
        storyPoints: 'Story points',
      },
      purged: 'Удалён навсегда',
    },
  },
  card: {
    label: 'Спринт',
    none: 'Без спринта',
    noTeam: 'Сначала выберите команду',
    loading: 'Загрузка…',
    failed: 'Не удалось загрузить спринты',
    active: (name: string) => `${name} · активен`,
    closed: (name: string) => `${name} · завершён`,
    cleared: 'Спринт будет очищен: он принадлежит прежней команде.',
  },
} as const;
