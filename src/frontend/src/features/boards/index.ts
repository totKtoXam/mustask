// Slice barrel (frontend.md "Slice rules"): what `app/` registers — the pages
// and their routes for the router (a team's two, mod-006, and the project's two,
// feat-039), and the landing and view builders and the read code for the
// navigation model's «Канбан» (mod-006's «Доски», renamed by feat-001 BR-H-16)
// and «Бэклоги» entries (spec § Navigation), with the project addresses the
// shell marks as those entries' pages (feat-039 BR-PB-9). `app/` imports this
// slice through this file only.
export { BacklogPage } from './pages/BacklogPage';
export { BoardPage } from './pages/BoardPage';
export { ProjectBacklogPage } from './pages/ProjectBacklogPage';
export { ProjectBoardPage } from './pages/ProjectBoardPage';
export {
  BACKLOG_ROUTE,
  BOARD_ROUTE,
  PROJECT_BACKLOG_ROUTE,
  PROJECT_BOARD_ROUTE,
  backlogLevelPaths,
  backlogsLandingPath,
  boardLevelPaths,
  boardsLandingPath,
  projectBacklogLevelOf,
  projectBacklogPath,
  projectBoardLevelOf,
  projectBoardPath,
} from './paths';
export { boardsPermissions } from './permissions';
