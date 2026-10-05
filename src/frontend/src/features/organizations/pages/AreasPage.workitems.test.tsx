import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AREA_URL, ids, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { renderPage } from '../testRender';

// mod-003 US-6: mod-002's «Удаление области» refuses an area that holds a work
// item with its existing code and text, unchanged (mod-003 api-contract § 6).
// «Отчёты» is a leaf and no team's default area, so mod-002's own checks pass
// and only the server's work-item check refuses it.

const PRM_AREAS = `/organizations/${ids.alfa}/projects/${ids.prm}/settings/areas`;

async function deleteReports() {
  const tree = await screen.findByRole('treegrid', { name: 'Области' });
  await userEvent.click(await within(tree).findByRole('button', { name: 'Действия: Отчёты' }));
  await userEvent.click(await screen.findByRole('menuitem', { name: 'Удалить' }));
  const dialog = await screen.findByRole('dialog', { name: 'Удаление области' });
  await userEvent.click(within(dialog).getByRole('button', { name: 'Удалить область' }));
  return { tree, dialog };
}

describe('mod-003 US-6 Scenario: An area holding a work item is not deleted', () => {
  it('shows «Нельзя удалить область, в которой есть рабочие элементы …» in the dialog, which stays; «Отчёты» stays in the tree', async () => {
    serveOrganizations();
    serveProblem('delete', AREA_URL, problem('organizations.area.not_empty'));
    renderPage({ path: PRM_AREAS });

    const { tree, dialog } = await deleteReports();

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Нельзя удалить область, в которой есть рабочие элементы (включая удалённые) или вложенные области',
    );
    expect(screen.getByRole('dialog', { name: 'Удаление области' })).toBeInTheDocument();
    expect(within(tree).getByText('Отчёты')).toBeInTheDocument();
  });
});

describe('mod-003 US-6 Scenario: The area is deleted once its items are moved out', () => {
  it('deletes «Отчёты» with mod-002’s notice once the server no longer refuses', async () => {
    serveOrganizations();
    renderPage({ path: PRM_AREAS });

    await deleteReports();

    expect(await screen.findByText('Область «Отчёты» удалена.')).toBeInTheDocument();
    expect(screen.queryByText('Отчёты')).not.toBeInTheDocument();
  });
});
