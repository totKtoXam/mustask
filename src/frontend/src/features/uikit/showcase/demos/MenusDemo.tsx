import { useState } from 'react';
import {
  ActionButton,
  MenuButton,
  MoreActionsMenu,
  SearchMenu,
  type MenuEntry,
} from '../../../../shared/ui';
import { DemoGrid, DemoGroup } from '../parts';
import { noop } from '../noop';

// Section 6, «Меню» (tech-015 design § 10.3): a text button whose menu lists pages, one
// whose menu picks a value, and the «⋮» with a group, a divider, a disabled and a
// destructive entry. The menus open for real.

const PAGES = ['Эпики', 'Фичи', 'Истории'] as const;
const SORTS = ['По порядку', 'По названию', 'По дате'] as const;

const MORE_ITEMS: readonly MenuEntry[] = [
  {
    group: 'Рабочий элемент',
    items: [
      { label: 'Копировать ссылку', onSelect: noop },
      { label: 'Дублировать', onSelect: noop },
    ],
  },
  'divider',
  { label: 'Архивировать', disabled: true },
  { label: 'Удалить', destructive: true, onSelect: noop },
];

const PROJECTS = [
  { id: 'a', label: 'Склад', hint: 'WH' },
  { id: 'b', label: 'Доставка', hint: 'DL' },
];
const OTHER_PROJECTS = [{ id: 'c', label: 'Бюджет', hint: 'FIN' }];

/** The header selector in its states: ready, loading, error. */
function SelectorDemo({ status }: { status: 'ready' | 'loading' | 'error' }) {
  const [project, setProject] = useState<string | null>(null);
  const chosen = [...PROJECTS, ...OTHER_PROJECTS].find((option) => option.id === project);
  return (
    <SearchMenu
      label="Проект"
      value={chosen?.label ?? 'все'}
      chosen={chosen !== undefined}
      currentId={project}
      allLabel="все"
      groups={[
        { options: PROJECTS },
        {
          heading: 'Остальные',
          note: 'Выбор сбросит то, что не подойдёт.',
          options: OTHER_PROJECTS,
        },
      ]}
      searchLabel="Найти…"
      emptyText="Ничего не найдено"
      status={status}
      errorText="Не удалось загрузить список. Попробуйте ещё раз."
      retryLabel="Повторить"
      onRetry={noop}
      onSelect={setProject}
    />
  );
}

export function MenusDemo() {
  const [sort, setSort] = useState<string>(SORTS[0]);
  return (
    <DemoGrid>
      <DemoGroup caption="Страницы одного вида (selection=page)">
        <MenuButton
          label="Уровень"
          selection="page"
          items={PAGES.map((name) => ({ label: name, current: name === 'Фичи' }))}
          trigger={(trigger) => (
            <ActionButton label="Фичи" menu={{ open: trigger['aria-expanded'] }} {...trigger} />
          )}
        />
      </DemoGroup>
      <DemoGroup caption="Выбор значения (selection=radio)">
        <MenuButton
          label="Порядок"
          selection="radio"
          items={SORTS.map((name) => ({
            label: name,
            current: name === sort,
            onSelect: () => setSort(name),
          }))}
          trigger={(trigger) => (
            <ActionButton label={sort} menu={{ open: trigger['aria-expanded'] }} {...trigger} />
          )}
        />
      </DemoGroup>
      <DemoGroup caption="Меню «⋮»: группа, разделитель, недоступный и опасный пункты">
        <MoreActionsMenu items={MORE_ITEMS} />
      </DemoGroup>
      <DemoGroup caption="Селектор с поиском (SearchMenu): готов">
        <SelectorDemo status="ready" />
      </DemoGroup>
      <DemoGroup caption="Селектор: загрузка">
        <SelectorDemo status="loading" />
      </DemoGroup>
      <DemoGroup caption="Селектор: ошибка с повтором">
        <SelectorDemo status="error" />
      </DemoGroup>
    </DemoGrid>
  );
}
