import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { VIEWER_CODES, servedWith } from '../../../test/organizationRoles';
import { serveWorkItems } from '../testApi';
import { ORGANIZATION_URL, emptyDataset, exampleDataset, ids } from '../testFixtures';
import { MSK_LIST, OPS_LIST, mskForm, projectContext, renderWorkItems } from '../testRender';

// mod-019 QA (US-4, BR-SA-18, OQ-11): a user whose organization role is «Читатель» and who holds the
// project role «Участник» on one project changes the items of that project in the panel, and only there:
// the work-item codes come from the project rows, once per project, never from the organization answer.

const PROJECT_MEMBER = [
  'boards.projectbacklog.reorder',
  'organizations.project.read',
  'workitems.link.write',
  'workitems.workitem.delete',
  'workitems.workitem.read',
  'workitems.workitem.write',
];
const UNION = [...new Set([...VIEWER_CODES, ...PROJECT_MEMBER])];

/** The project rows: «Мастаск» carries the grant's codes, «Операции» the reader's alone. */
function serveGrantOnMsk() {
  server.use(
    http.get(`${ORGANIZATION_URL}/projects`, () =>
      HttpResponse.json([
        { id: ids.msk, name: 'Мастаск', key: 'MSK', codes: UNION },
        { id: ids.ops, name: 'Операции', key: 'OPS', codes: [...VIEWER_CODES] },
      ]),
    ),
  );
}

describe('mod-019 QA: an organization reader holding a project «Участник» grant', () => {
  it('is offered «Новый рабочий элемент» on the granted project', async () => {
    serveWorkItems();
    const shell = renderWorkItems(MSK_LIST, { role: servedWith('viewer', UNION) });
    serveGrantOnMsk();

    expect(await screen.findByRole('table', { name: 'Рабочие элементы' })).toBeVisible();
    await waitFor(() => expect(shell.queryClient.isFetching()).toBe(0));
    expect(await screen.findAllByRole('button', { name: 'Новый рабочий элемент' })).toHaveLength(1);
  });

  it('is offered nothing on another project of the same organization', async () => {
    serveWorkItems(emptyDataset());
    const shell = renderWorkItems(OPS_LIST, {
      role: servedWith('viewer', UNION),
      context: projectContext({ projectId: ids.ops }),
    });
    serveGrantOnMsk();

    expect(
      await screen.findByRole('heading', { level: 2, name: 'Рабочих элементов пока нет' }),
    ).toBeVisible();
    await waitFor(() => expect(shell.queryClient.isFetching()).toBe(0));
    expect(screen.queryAllByRole('button', { name: 'Новый рабочий элемент' })).toHaveLength(0);
  });

  it('edits an item card of the granted project, and the assignee search names the project', async () => {
    const api = serveWorkItems(exampleDataset());
    renderWorkItems(mskForm('MSK-BUG-4'), { role: servedWith('viewer', UNION) });
    serveGrantOnMsk();

    const title = await screen.findByRole('textbox', { name: 'Заголовок' });
    await waitFor(() => expect(title).not.toHaveAttribute('readonly'));
    await userEvent.type(title, ' дополнение');
    expect(await screen.findByRole('button', { name: 'Сохранить' })).toBeEnabled();

    const assignee = screen.getByRole('combobox', { name: 'Исполнитель' });
    await userEvent.click(assignee);
    await userEvent.clear(assignee);
    await userEvent.paste('Кузнецова');
    await waitFor(() =>
      expect(
        api.reads('assignee-candidates').some((read) => read.path.includes(`projectId=${ids.msk}`)),
      ).toBe(true),
    );
  });

  it('reads an item of the project it holds nothing on, as a value', async () => {
    serveWorkItems(exampleDataset());
    renderWorkItems(mskForm('MSK-BUG-4'), { role: servedWith('viewer', [...VIEWER_CODES]) });

    const title = await screen.findByRole('textbox', { name: 'Заголовок' });
    expect(title).toHaveAttribute('readonly');
    expect(screen.queryByRole('button', { name: 'Сохранить' })).not.toBeInTheDocument();
  });
});
