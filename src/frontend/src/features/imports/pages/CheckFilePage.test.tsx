import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { importErrorCodes } from '../../../shared/api/errorCodes';
import { apiUrl, problemText, serveProblem } from '../../../test/api';
import { server } from '../../../test/mocks/server';
import { renderPage } from '../../../test/renderPage';
import { texts } from '../lib/texts';
import {
  BASE,
  IMPORTS_URL,
  TEMPLATE_ID,
  dryRun,
  serveImports,
  templateItem,
  templateList,
} from '../testFixtures';

const CHECK = `${BASE}/check?template=${TEMPLATE_ID}`;

function serveUpload() {
  server.use(
    http.post(apiUrl(`${IMPORTS_URL}/files`), () =>
      HttpResponse.json({
        fileId: 'file-1',
        fileName: 'tasks.xlsx',
        size: 1,
        expiresAt: '',
        read: {},
      }),
    ),
  );
}

async function pickFile() {
  const input = document.querySelector('input[type="file"]') as HTMLInputElement;
  await userEvent.upload(input, new File(['x'], 'tasks.xlsx'));
}

async function runCheck() {
  await screen.findByRole('button', { name: texts.run });
  await pickFile();
  await waitFor(() => expect(screen.getByRole('button', { name: texts.run })).toBeEnabled());
  await userEvent.click(screen.getByRole('button', { name: texts.run }));
}

describe('the file check', () => {
  it('asks for a template and a file first', async () => {
    serveImports();
    renderPage(CHECK);
    expect(await screen.findByText(texts.runHeading)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: texts.run })).toBeDisabled();
  });

  it('runs the check and shows the totals and the rows', async () => {
    serveImports();
    serveUpload();
    server.use(
      http.post(apiUrl(`${IMPORTS_URL}/templates/:id/dry-runs`), () => HttpResponse.json(dryRun())),
      http.get(apiUrl(`${IMPORTS_URL}/dry-runs/:id`), () => HttpResponse.json(dryRun())),
    );
    renderPage(CHECK);
    await runCheck();
    expect(await screen.findByText('Первая задача')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: `${texts.results.error}: 1` })).toBeInTheDocument();
    expect(screen.getByText('Название не заполнено.')).toBeInTheDocument();
    expect(screen.getByText(texts.previewCaption)).toBeInTheDocument();
  });

  it('asks the server again for the rows of the chosen result', async () => {
    serveImports();
    serveUpload();
    const asked: string[] = [];
    server.use(
      http.post(apiUrl(`${IMPORTS_URL}/templates/:id/dry-runs`), () => HttpResponse.json(dryRun())),
      http.get(apiUrl(`${IMPORTS_URL}/dry-runs/:id`), ({ request }) => {
        const result = new URL(request.url).searchParams.get('result') ?? '';
        asked.push(result);
        const rows = dryRun().rows.filter((row) => result === 'all' || row.result === result);
        return HttpResponse.json(dryRun({ result: result as never, rows }));
      }),
    );
    renderPage(CHECK);
    await runCheck();
    await screen.findByText('Первая задача');
    await userEvent.click(screen.getByRole('button', { name: `${texts.results.error}: 1` }));
    await waitFor(() => expect(asked).toContain('error'));
    await waitFor(() => expect(screen.queryByText('Первая задача')).toBeNull());
  });

  it('says a note of the report by the catalogue text', async () => {
    serveImports();
    serveUpload();
    const withNote = dryRun({ notes: ['imports.dry_run.no_identifier_column'] });
    server.use(
      http.post(apiUrl(`${IMPORTS_URL}/templates/:id/dry-runs`), () => HttpResponse.json(withNote)),
      http.get(apiUrl(`${IMPORTS_URL}/dry-runs/:id`), () => HttpResponse.json(withNote)),
    );
    renderPage(CHECK);
    await runCheck();
    expect(
      await screen.findByText(problemText('imports.dry_run.no_identifier_column')),
    ).toBeInTheDocument();
  });

  it('says the file was refused and keeps the check disabled until another file', async () => {
    serveImports();
    serveProblem('post', `${IMPORTS_URL}/files`, importErrorCodes.fileRefused);
    renderPage(CHECK);
    await runCheck();
    expect(await screen.findByText(new RegExp(texts.fileRefused))).toBeInTheDocument();
  });

  it('shows the failure of the run with a retry', async () => {
    serveImports();
    serveUpload();
    serveProblem(
      'post',
      `${IMPORTS_URL}/templates/:id/dry-runs`,
      importErrorCodes.templateNotFound,
    );
    renderPage(CHECK);
    await runCheck();
    const banner = await screen.findByText(texts.runFailed);
    expect(
      within(banner.closest('[role="alert"]') ?? document.body).getByRole('button', {
        name: 'Повторить',
      }),
    ).toBeInTheDocument();
  });

  it('denies the page to who may run no template', async () => {
    serveImports({ list: templateList([templateItem({ canRun: false })], { canRun: false }) });
    renderPage(CHECK);
    expect(await screen.findByText('Доступ ограничен')).toBeInTheDocument();
  });
});
