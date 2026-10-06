import type { PageView } from '../../../shared/ui/PageHeader';
import { BOARD_LEVELS, LEVEL_LABELS, type BoardLevelCode } from './levels';

/** The header's tabs «Эпики», «Фичи», «Истории», each its own address (spec § Screens and copy). */
export function levelViews(pathOf: (level: BoardLevelCode) => string): PageView[] {
  return BOARD_LEVELS.map((level) => ({ label: LEVEL_LABELS[level], to: pathOf(level) }));
}
