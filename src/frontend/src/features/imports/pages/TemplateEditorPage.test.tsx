import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { importErrorCodes } from '../../../shared/api/errorCodes';
import { recordRequests, serveProblem } from '../../../test/api';
import { renderPage } from '../../../test/renderPage';
import { texts } from '../lib/texts';
import {
  BASE,
  IMPORTS_URL,
  TEMPLATE_ID,
  serveImports,
  template,
  templateList,
  templateItem,
} from '../testFixtures';

const SAVED = `${BASE}/templates/${TEMPLATE_ID}`;

describe('the template editor', () => {
  it('opens a saved template with its name and the four tabs', async () => {
    serveImports();
    renderPage(SAVED);
    expect(await screen.findByDisplayValue('Выгрузка задач')).toBeInTheDocument();
    for (const name of Object.values(texts.tabs)) {
      expect(await screen.findByRole('tab', { name: new RegExp(name) })).toBeInTheDocument();
    }
  });

  it('saves a changed name with the whole template and says it', async () => {
    serveImports();
    const { requests } = recordRequests('put', `${IMPORTS_URL}/templates/:id`, () =>
      template({ name: 'Выгрузка 2', version: 2 }),
    );
    renderPage(SAVED);
    const name = await screen.findByDisplayValue('Выгрузка задач');
    await userEvent.clear(name);
    await userEvent.type(name, 'Выгрузка 2');
    await userEvent.click(screen.getByRole('button', { name: texts.save }));
    expect(await screen.findByText(texts.saved)).toBeInTheDocument();
    expect(requests).toHaveLength(1);
    expect(requests[0]?.body).toMatchObject({ name: 'Выгрузка 2' });
  });

  it('puts a taken name on the name field', async () => {
    serveImports();
    const body = serveProblem(
      'put',
      `${IMPORTS_URL}/templates/:id`,
      importErrorCodes.templateNameTaken,
      {
        name: 'Выгрузка задач',
      },
    );
    renderPage(SAVED);
    const name = await screen.findByDisplayValue('Выгрузка задач');
    await userEvent.type(name, ' x');
    await userEvent.click(screen.getByRole('button', { name: texts.save }));
    expect(await screen.findByText(body.detail)).toBeInTheDocument();
  });

  it('does not offer saving a template of another author to who can not edit it', async () => {
    serveImports({ template: template({ canEdit: false, canDelete: false, canCopy: false }) });
    renderPage(SAVED);
    await screen.findByDisplayValue('Выгрузка задач');
    expect(screen.queryByRole('button', { name: texts.save })).toBeNull();
    expect(screen.getByText(texts.readOnlyNote('Анна Пример'))).toBeInTheDocument();
  });

  it('says a template is not found', async () => {
    serveImports();
    serveProblem('get', `${IMPORTS_URL}/templates/:id`, importErrorCodes.templateNotFound);
    renderPage(SAVED);
    expect(await screen.findByText(texts.templateNotFound)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: texts.toList })).toBeInTheDocument();
  });

  it('asks for a file before the mapping of a new template', async () => {
    serveImports({ list: templateList([templateItem()]) });
    renderPage(`${BASE}/templates/new`);
    const mapping = await screen.findByRole('tab', { name: new RegExp(texts.tabs.mapping) });
    await userEvent.click(mapping);
    expect(await screen.findByText(texts.uploadFirst)).toBeInTheDocument();
  });

  it('denies a new template to who may not create one', async () => {
    serveImports({ list: templateList([], { canCreate: false }) });
    renderPage(`${BASE}/templates/new`);
    await waitFor(() => expect(screen.getByText('Доступ ограничен')).toBeInTheDocument());
  });
});
