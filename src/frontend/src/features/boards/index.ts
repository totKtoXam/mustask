// Slice barrel (frontend.md "Slice rules"): what `app/` registers — the two
// pages and their routes for the router, and the landing and view builders and
// the read code for the navigation model's «Канбан» (mod-006's «Доски», renamed
// by feat-001 BR-H-16) and «Бэклоги» entries (spec § Navigation). `app/` imports this slice through this file only.
export { BacklogPage } from './pages/BacklogPage';
export { BoardPage } from './pages/BoardPage';
export {
  BACKLOG_ROUTE,
  BOARD_ROUTE,
  backlogLevelPaths,
  backlogsLandingPath,
  boardLevelPaths,
  boardsLandingPath,
} from './paths';
export { boardsPermissions } from './permissions';
