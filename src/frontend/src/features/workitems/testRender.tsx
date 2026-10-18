import { Route } from 'react-router-dom';
import { vi } from 'vitest';
import type { ServedAccess, TestRole } from '../../test/organizationRoles';
import { authValue, renderShell } from '../../test/renderShell';
import type { WorkContextSelection, WorkContextSource } from '../../shared/lib/workContext';
import { OrganizationGate } from '../organizations';
import { WorkItemFormPage } from './pages/WorkItemFormPage';
import { WorkItemsPage } from './pages/WorkItemsPage';
import { WORK_ITEMS_ROUTE, WORK_ITEM_ROUTE } from './paths';
import { ids } from './testFixtures';

/** The list of «Мастаск» in «Пример организации». */
export const MSK_LIST = `/organizations/${ids.example}/projects/${ids.msk}/workitems`;
export const OPS_LIST = `/organizations/${ids.example}/projects/${ids.ops}/workitems`;

/** A form's address in «Мастаск». */
export function mskForm(identifier: string): string {
  return `${MSK_LIST}/${identifier}`;
}

/**
 * A real context with «Пример организации» / «Мастаск» chosen: the shell
 * shows the project-scoped «Рабочие элементы» and derives the form's trail.
 */
export function projectContext(selection: WorkContextSelection = {}): WorkContextSource {
  return {
    status: 'ready',
    organizations: [
      {
        id: ids.example,
        name: 'Пример организации',
        projects: [
          { id: ids.msk, name: 'Мастаск' },
          { id: ids.ops, name: 'Операции' },
        ],
        teams: [],
      },
    ],
    selection: { organizationId: ids.example, projectId: ids.msk, ...selection },
    isReal: true,
    select: vi.fn(),
  };
}

/**
 * The slice's two routes, as `app/routes.tsx` registers them, under the
 * organization gate as there (mod-016): a page asks `useAccessCheck()` for the
 * organization in its address, and the shell primes that organization's codes
 * by the `role` of `renderWorkItems`. The pages are mounted without
 * `OrganizationPage`, so no test meets the gate's first rows.
 */
export function workItemRoutes() {
  return (
    <>
      <Route element={<OrganizationGate />}>
        <Route path={WORK_ITEMS_ROUTE} element={<WorkItemsPage />} />
        <Route path={WORK_ITEM_ROUTE} element={<WorkItemFormPage />} />
      </Route>
      <Route path="*" element={<p>Другая страница</p>} />
    </>
  );
}

/**
 * Opens `path` inside the real shell (`AppLayout`, its navigation model and
 * page chrome) with a stub context and session, as `app/routes.tsx` mounts the
 * slice: the page under test is the one `path` opens, and a flow moves through
 * links as the product does.
 */
export function renderWorkItems(
  path: string,
  options: {
    context?: WorkContextSource;
    /**
     * The caller's role in the selected organization (mod-016): the administrator
     * by default; a `ServedAccess` is a bundle of exactly its codes.
     */
    role?: TestRole | ServedAccess;
  } = {},
) {
  return renderShell({
    path,
    auth: authValue([]),
    contextSource: options.context ?? projectContext(),
    routes: workItemRoutes(),
    access: options.role ?? 'admin',
  });
}
