import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { importErrorCodes } from '../../../shared/api/errorCodes';
import { apiUrl, recordRequests, serveProblem } from '../../../test/api';
import { server } from '../../../test/mocks/server';
import { renderPage } from '../../../test/renderPage';
import { texts } from '../lib/texts';
import {
  BASE,
  IMPORTS_URL,
  OTHER_TEMPLATE_ID,
  serveImports,
  templateItem,
  templateList,
} from '../testFixtures';

function open() {
  return renderPage(BASE);
}

describe('the template list', () => {
  it('shows a template with its source and the author', async () => {
    serveImports();
    open();
    expect(await screen.findByText('Выгрузка задач')).toBeInTheDocument();
    expect(screen.getByText('Анна Пример')).toBeInTheDocument();
    expect(screen.getByText('.xlsx · лист «Лист1»')).toBeInTheDocument();
  });

  it('offers only what the row flags allow', async () => {
    serveImports({
      list: templateList([
        templateItem({ canEdit: false, canDelete: false, canCopy: false, canRun: true }),
      ]),
    });
    open();
    await screen.findByText('Выгрузка задач');
    expect(screen.queryByRole('button', { name: texts.copy('Выгрузка задач') })).toBeNull();
    expect(screen.queryByRole('button', { name: texts.remove('Выгрузка задач') })).toBeNull();
    expect(screen.getByRole('button', { name: texts.open('Выгрузка задач') })).toBeInTheDocument();
  });

  it('shows a member only the columns of a template he can not open', async () => {
    serveImports({
      list: templateList(
        [templateItem({ canEdit: false, canDelete: false, canCopy: false, canRun: false })],
        { canCreate: false, canRun: false },
      ),
    });
    server.use(
      http.get(apiUrl(`${IMPORTS_URL}/templates/:id/columns`), () =>
        HttpResponse.json({ id: 'x', name: 'Выгрузка задач', columns: ['Название', 'Статус'] }),
      ),
    );
    open();
    await screen.findByText('Выгрузка задач');
    expect(screen.queryByRole('link', { name: 'Выгрузка задач' })).toBeNull();
    expect(screen.queryByRole('link', { name: texts.createTemplate })).toBeNull();
    await userEvent.click(
      screen.getByRole('button', { name: `${texts.columnsOf}: Выгрузка задач` }),
    );
    expect(await screen.findByText('Статус')).toBeInTheDocument();
  });

  it('says the list is empty and offers the first template to who may create', async () => {
    serveImports({ list: templateList([]) });
    open();
    expect(await screen.findByText(texts.emptyHeading)).toBeInTheDocument();
    expect(screen.getByText(texts.emptyHintCreate)).toBeInTheDocument();
  });

  it('shows the load error with a retry', async () => {
    serveImports();
    serveProblem('get', `${IMPORTS_URL}/templates`, importErrorCodes.accessForbidden);
    open();
    expect(await screen.findByRole('button', { name: 'Повторить' })).toBeInTheDocument();
  });

  it('copies a template and says it', async () => {
    serveImports({ list: templateList([templateItem()]) });
    recordRequests('post', `${IMPORTS_URL}/templates/:id/copy`, () => ({
      id: OTHER_TEMPLATE_ID,
      name: 'Выгрузка задач (копия)',
    }));
    open();
    await screen.findByText('Выгрузка задач');
    await userEvent.click(screen.getByRole('button', { name: texts.copy('Выгрузка задач') }));
    expect(await screen.findByText(texts.copied('Выгрузка задач (копия)'))).toBeInTheDocument();
  });

  it('says the limit when a copy is refused for it', async () => {
    serveImports();
    serveProblem(
      'post',
      `${IMPORTS_URL}/templates/:id/copy`,
      importErrorCodes.templateLimitReached,
    );
    open();
    await screen.findByText('Выгрузка задач');
    await userEvent.click(screen.getByRole('button', { name: texts.copy('Выгрузка задач') }));
    expect(await screen.findByText(texts.limitReached)).toBeInTheDocument();
  });

  it('deletes after the confirmation, and treats a missing template as deleted', async () => {
    serveImports();
    serveProblem('delete', `${IMPORTS_URL}/templates/:id`, importErrorCodes.templateNotFound);
    open();
    await screen.findByText('Выгрузка задач');
    await userEvent.click(screen.getByRole('button', { name: texts.remove('Выгрузка задач') }));
    await userEvent.click(await screen.findByRole('button', { name: texts.delete }));
    await waitFor(() => expect(screen.getByText(texts.alreadyDeleted)).toBeInTheDocument());
  });
});
