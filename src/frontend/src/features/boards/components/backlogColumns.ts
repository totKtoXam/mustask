// The backlog table's headers (spec § Page «Бэклоги»; BR-M4-14; feat-001
// spec § Page «Бэклоги»: «Родитель» after «Заголовок», and «Действия» on the
// levels whose rows can be given a parent), shared by the table and the
// page's loading skeleton.
import { levelAbove, type BoardLevelCode } from '../lib/levels';

/** The handle column's header: visually hidden, read by assistive technology. */
export const HANDLE_HEADER = 'Перемещение';

export const ORDER_HEADER = 'Порядок';

/** The headers after «Порядок» and the handle. */
export const ITEM_COLUMNS = [
  'ID',
  'Тип',
  'Заголовок',
  'Родитель',
  'Исполнитель',
  'Статус',
] as const;

/** feat-001: the row menu's column on «Фичи» and «Истории», its header visually hidden. */
export const ACTIONS_HEADER = 'Действия';

/** Whether the level's rows have «Ещё действия» (BR-H-14): there is a level above it. */
export function hasRowActions(level: BoardLevelCode): boolean {
  return levelAbove(level) !== null;
}

/**
 * The skeleton's headers: the table's own, the handle's and the actions'
 * left blank, as they are hidden on the table too.
 */
export function backlogSkeletonColumns(level: BoardLevelCode): string[] {
  return [ORDER_HEADER, '', ...ITEM_COLUMNS, ...(hasRowActions(level) ? [''] : [])];
}
