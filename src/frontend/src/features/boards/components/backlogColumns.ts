// The backlog table's headers (spec § Page «Бэклоги»; BR-M4-14), shared by the
// table and the page's loading skeleton.

/** The handle column's header: visually hidden, read by assistive technology. */
export const HANDLE_HEADER = 'Перемещение';

export const ORDER_HEADER = 'Порядок';

/** The headers after «Порядок» and the handle. */
export const ITEM_COLUMNS = ['ID', 'Тип', 'Заголовок', 'Исполнитель', 'Статус'] as const;

/**
 * The skeleton's headers: the table's own, the handle's left blank as it is
 * hidden on the table too.
 */
export const BACKLOG_SKELETON_COLUMNS = [ORDER_HEADER, '', ...ITEM_COLUMNS];
