import { Routes } from 'react-router-dom';
import { routeTree } from './routeTree';

/** The panel's routes: the table is `routeTree.tsx`, which the route-inventory test reads too. */
export function AppRoutes() {
  return <Routes>{routeTree}</Routes>;
}
