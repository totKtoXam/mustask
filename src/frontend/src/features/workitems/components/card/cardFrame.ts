import type { ReactNode } from 'react';
import type { CardTabSlot } from './cardSlots';

/**
 * What the card hands its shell (feat-029 design § 4): the content of one card, named
 * by the kit part that arranges each piece. `WorkItemCard` builds the content from
 * the kit's fields, buttons and marks; a *frame* — `PageFrame` for the page,
 * `DialogFrame` for the dialog, both in `KitFrames.tsx` — puts it in the kit's
 * shell, header bar, band, tabs, columns and sections. The two shells differ; the
 * mapping does not, so the card is written once. The page's frame is drawn by the card's
 * own `renderFrame`; the dialog's shell is the host's, drawn before the card exists, and
 * the card publishes its frame into it (`PublishFrame`, `frameStore.ts`).
 */

/** A titled group of a column (the kit's `Section`). */
export interface CardSection {
  key: string;
  /** The `h2` of the section; the section is a `region` named by it. */
  title: string;
  content: ReactNode;
  /**
   * A head of level 3 under the content, with its own content: «Дочерние
   * элементы (n)» (the kit's `SectionHead headingLevel={3} count divider={false}`,
   * `id` the head's id that the content's list is named by).
   */
  subsection?: { id: string; title: string; count: number; content: ReactNode };
}

/** What a column holds, top to bottom: a section, or a slot's own node (its owner draws its head). */
export type CardColumnItem = CardSection | { key: string; node: ReactNode };

/** The card's error or info notice, between the band and the body (the kit's `InlineNotice`). */
export interface CardNotice {
  severity: 'error' | 'info';
  text: string;
  /** A second line under the text: a deleted card says who deleted the item and when (mod-005 BR-DR-7). */
  detail?: string;
}

export interface CardFrameProps {
  mode: 'dialog' | 'page';
  // ---- CardHeaderBar
  /** The type's appearance token: the bar's colour and the type line's icon. */
  appearance: string;
  /** «Пользовательская история»; the type line reads «<typeName> <identifier>». */
  typeName: string;
  identifier: string;
  /** The type line's id: the dialog's `labelledBy`. The type line is the heading, `h1` on the page, `h2` in the dialog. */
  headingId: string;
  /** Row 2, after the identifier: the title field. */
  title: ReactNode;
  /** Row 3, left: the assignee and the places kept after it. */
  headerMeta: ReactNode;
  /** Row 3, right: the places kept, the Save group, «Обновить», «Отменить все изменения», «Ещё действия». */
  headerActions: ReactNode;
  // ---- PropertyBand
  /** Two blocks of fields with `labelPlacement="start"`: «Статус» + a place kept, «Область» + a place kept. */
  bandBlocks: [ReactNode, ReactNode];
  /** The stamp at the top right: «Изменён: <дата>» or the place kept for «Обновил <имя>». */
  bandMeta: ReactNode;
  /** The tab strip: its name, the one tab the product has, and the places kept for the tabs of other tasks. */
  tabs: { label: string; details: string; extra: readonly CardTabSlot[] };
  // ---- the alert area
  notice: CardNotice | null;
  // ---- ColumnLayout
  /** The texts, then «Обсуждение». */
  main: readonly CardColumnItem[];
  /** The type's sections. */
  side: readonly CardColumnItem[];
  /** «Развёртывание», «Разработка», then «Связанная работа». */
  extra: readonly CardColumnItem[];
  // ---- the shell
  /** A save is running: the dialog is not dismissible and its icons are inert. */
  busy: boolean;
  /** Edits are unsaved: the dialog's `guard.dirty`. */
  dirty: boolean;
  /** The item's own page: where the dialog's type line leads, in a new tab. */
  pagePath: string;
}
