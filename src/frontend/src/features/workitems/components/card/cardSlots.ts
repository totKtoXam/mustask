import type { ReactNode } from 'react';
import type { BoxedTab } from '../../../../shared/ui';

/** A tab of another task's: its entry in the strip (icon, label, count) and the panel it opens. */
export interface CardTabSlot {
  tab: BoxedTab;
  panel: ReactNode;
}

/**
 * The places the card keeps for the reference's parts the product does not have
 * yet (feat-029 D-1, BR-C-18; design § 4.8). A slot is a node, absent by
 * default: the card draws nothing there — no disabled stand-in, no empty box —
 * and the owner's task passes a component and nothing else moves.
 */
export interface WorkItemCardSlots {
  /** Header row 3, after the assignee: the comments count. Owner: feat-002. */
  commentsCount?: ReactNode;
  /** Header row 3, after `commentsCount`: the tags and their «+». Owner: feat-016. */
  tags?: ReactNode;
  /** Header row 3, left of «Обновить»: «Следить». No task queued (release 2, doc-011). */
  follow?: ReactNode;
  /** The band's stamp: «Обновил <имя>: <дата>» replaces «Изменён: <дата>». Owner: feat-015. */
  updatedBy?: ReactNode;
  /** Band, block A, row 2: «Причина». No task queued (release 3, doc-002 D-6). */
  reason?: ReactNode;
  /** Band, block B, row 2: «Итерация». Owner: mod-007. */
  iteration?: ReactNode;
  /** The tab strip, left of «Детали»: «История», «Вложения». Owners: feat-015, mod-004. */
  tabs?: readonly CardTabSlot[];
  /** First column, under the type's texts: «Обсуждение». Owner: feat-002. */
  discussion?: ReactNode;
  /** Third column, above «Связанная работа»: «Развёртывание». No task queued (feat-022 D-2). */
  deployment?: ReactNode;
  /** Third column, above «Связанная работа»: «Разработка». No task queued (feat-022 D-2). */
  development?: ReactNode;
}
