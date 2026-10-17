// The backlog table's headers (feat-027 BR-T-1; design § 4.3), shared by the
// table and the page's loading skeleton: the hierarchy column, «Порядок»,
// «Тип», «Заголовок», «Статус», «Команда» (mod-017 design § 6.4), «Трудоёмкость»,
// «Бизнес-ценность», «Вид ценности» — and no more. The hierarchy column's header holds the two level
// buttons; it is named by an `aria-label` on the cell, not by its content.

/** The hierarchy column's name: an `aria-label` on its header cell (design § 4.5). */
export const HIERARCHY_HEADER = 'Иерархия';

export const ORDER_HEADER = 'Порядок';
export const TYPE_HEADER = 'Тип';
export const TITLE_HEADER = 'Заголовок';
export const STATE_HEADER = 'Статус';
export const TEAM_HEADER = 'Команда';
export const EFFORT_HEADER = 'Трудоёмкость';
export const BUSINESS_VALUE_HEADER = 'Бизнес-ценность';
export const VALUE_AREA_HEADER = 'Вид ценности';

/** The hierarchy header's two buttons (BR-T-8). */
export const EXPAND_ONE_LEVEL_LABEL = 'Развернуть на один уровень';
export const COLLAPSE_ONE_LEVEL_LABEL = 'Свернуть на один уровень';

/**
 * The skeleton's headers: the table's own, the hierarchy header left blank —
 * its buttons are the table's (design § 4.12).
 */
export function backlogSkeletonColumns(): string[] {
  return [
    '',
    ORDER_HEADER,
    TYPE_HEADER,
    TITLE_HEADER,
    STATE_HEADER,
    TEAM_HEADER,
    EFFORT_HEADER,
    BUSINESS_VALUE_HEADER,
    VALUE_AREA_HEADER,
  ];
}
