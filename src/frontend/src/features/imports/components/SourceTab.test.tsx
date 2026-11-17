import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { importErrorCodes } from '../../../shared/api/errorCodes';
import { apiUrl, problem, problemText } from '../../../test/api';
import { server } from '../../../test/mocks/server';
import { renderPage } from '../../../test/renderPage';
import { PROCESS_DEFINITION_URL, sampleProcessDefinition } from '../../process/testFixtures';
import { texts } from '../lib/texts';
import {
  BASE,
  IMPORTS_URL,
  TEMPLATE_ID,
  fileRead,
  serveFile,
  serveImports,
  template,
} from '../testFixtures';

const NEW = `${BASE}/templates/new`;

async function pick(name = 'tasks.xlsx') {
  const input = await waitFor(() => {
    const found = document.querySelector('input[type="file"]');
    if (!found) throw new Error('no file input yet');
    return found as HTMLInputElement;
  });
  await userEvent.upload(input, new File(['x'], name));
}

function serveRefusal(reason: string) {
  const base = problem(importErrorCodes.fileRefused);
  server.use(
    http.post(apiUrl(`${IMPORTS_URL}/files`), () =>
      HttpResponse.json(
        { ...base, errors: { file: [{ code: reason, detail: problemText(reason) }] } },
        { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
      ),
    ),
  );
}

describe('the source tab: the sample file', () => {
  it('reads a picked file and previews its headers with the cell addresses and the first rows', async () => {
    serveImports();
    serveFile();
    renderPage(NEW);
    await pick();
    expect(await screen.findByText(/tasks\.xlsx · 20 КБ/)).toBeInTheDocument();
    const preview = await screen.findByRole('table', { name: texts.previewLabel });
    expect(within(preview).getByText('Название (A1)')).toBeInTheDocument();
    expect(within(preview).getByText('Статус (B1)')).toBeInTheDocument();
    expect(within(preview).getByText('Первая задача')).toBeInTheDocument();
  });

  it('sends the file as a multipart body', async () => {
    serveImports();
    let contentType = '';
    server.use(
      http.post(apiUrl(`${IMPORTS_URL}/files`), ({ request }) => {
        contentType = request.headers.get('content-type') ?? '';
        return HttpResponse.json({
          fileId: 'f',
          fileName: 'tasks.xlsx',
          size: 1,
          expiresAt: '',
          read: fileRead(),
        });
      }),
    );
    renderPage(NEW);
    await pick();
    await screen.findByRole('table', { name: texts.previewLabel });
    expect(contentType).toContain('multipart/form-data');
  });

  it.each([
    'imports.file.unsupported_format',
    'imports.file.too_large',
    'imports.file.too_many_rows',
    'imports.file.too_many_columns',
    'imports.file.too_many_sheets',
    'imports.file.expands_too_much',
    'imports.file.external_reference',
    'imports.file.has_macros',
    'imports.file.unreadable',
  ])('refuses the file for %s with the catalogue text and no preview', async (reason) => {
    serveImports();
    serveRefusal(reason);
    renderPage(NEW);
    await pick('broken.xlsx');
    expect(await screen.findByText(texts.fileRefused)).toBeInTheDocument();
    expect(screen.getByText(problemText(reason))).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: texts.previewLabel })).toBeNull();
  });

  it('keeps the template untouched when a second file is refused', async () => {
    serveImports();
    serveFile();
    renderPage(NEW);
    await pick();
    await screen.findByRole('table', { name: texts.previewLabel });
    serveRefusal('imports.file.too_large');
    await pick('big.xlsx');
    expect(await screen.findByText(problemText('imports.file.too_large'))).toBeInTheDocument();
    expect(screen.getByRole('table', { name: texts.previewLabel })).toBeInTheDocument();
  });

  it('removes the file and says the sample is no longer stored', async () => {
    serveImports();
    serveFile();
    renderPage(NEW);
    await pick();
    await screen.findByRole('table', { name: texts.previewLabel });
    await userEvent.click(screen.getByRole('button', { name: texts.removeFile }));
    expect(screen.queryByText(/tasks\.xlsx · /)).toBeNull();
    expect(screen.getByText(texts.sampleExpired)).toBeInTheDocument();
  });

  it('reads the columns again for another sheet', async () => {
    serveImports();
    serveFile();
    renderPage(NEW);
    await pick();
    await screen.findByRole('table', { name: texts.previewLabel });
    const asked: string[] = [];
    server.use(
      http.get(apiUrl(`${IMPORTS_URL}/files/:id/columns`), ({ request }) => {
        asked.push(new URL(request.url).search);
        return HttpResponse.json(fileRead({ sheet: 'Лист2' }));
      }),
    );
    await userEvent.click(screen.getByRole('combobox', { name: texts.sheet }));
    await userEvent.click(await screen.findByRole('option', { name: 'Лист2' }));
    await waitFor(() => expect(asked).toHaveLength(1));
    expect(asked[0]).toContain('sheet=%D0%9B%D0%B8%D1%81%D1%822');
  });

  // QA-FD-1 (open): the blur of the header-row / first-data-row field never re-reads the columns.
  it('says a header row without headers under the field (QA-FD-1)', async () => {
    serveImports();
    serveFile();
    renderPage(NEW);
    await pick();
    await screen.findByRole('table', { name: texts.previewLabel });
    server.use(
      http.get(apiUrl(`${IMPORTS_URL}/files/:id/columns`), () => {
        const body = problem(importErrorCodes.headerEmpty);
        return HttpResponse.json(body, {
          status: body.status,
          headers: { 'Content-Type': 'application/problem+json' },
        });
      }),
    );
    const row = screen.getByRole('textbox', { name: new RegExp(texts.headerRow) });
    await userEvent.clear(row);
    await userEvent.type(row, '9');
    await userEvent.tab();
    expect(await screen.findByText(texts.noHeaders)).toBeInTheDocument();
  });

  it('offers the encoding and the delimiter for a csv and no sheet', async () => {
    serveImports();
    serveFile(
      fileRead({
        format: 'csv',
        sheets: [],
        sheet: null,
        encoding: 'utf-8',
        delimiter: ';',
        encodingDetected: true,
        delimiterDetected: true,
      }),
    );
    renderPage(NEW);
    await pick('tasks.csv');
    await screen.findByRole('table', { name: texts.previewLabel });
    expect(screen.getByRole('combobox', { name: texts.encoding })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: texts.delimiter })).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: texts.sheet })).toBeNull();
  });

  it('proposes the mapping of the picked file on the mapping tab', async () => {
    serveImports();
    serveFile();
    server.use(
      http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(sampleProcessDefinition)),
      http.get(apiUrl('/api/organizations/:organizationId/members'), () => HttpResponse.json([])),
    );
    renderPage(NEW);
    await pick();
    await screen.findByRole('table', { name: texts.previewLabel });
    await userEvent.click(screen.getByRole('tab', { name: new RegExp(texts.tabs.mapping) }));
    const table = await screen.findByRole('table', { name: texts.mappingTable });
    expect(within(table).getAllByRole('row')).toHaveLength(3);
  });

  it('offers no file picker in a read-only template', async () => {
    serveImports({ template: template({ canEdit: false, canDelete: false }) });
    renderPage(`${BASE}/templates/${TEMPLATE_ID}`);
    await screen.findByDisplayValue('Выгрузка задач');
    expect(document.querySelector('input[type="file"]')).toBeNull();
  });
});
