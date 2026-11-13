import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { apiUrl, problem, problemText } from '../../../test/api';
import { server } from '../../../test/mocks/server';
import { renderPage } from '../../../test/renderPage';
import { texts } from '../lib/texts';
import {
  BASE,
  IMPORTS_URL,
  TEMPLATE_ID,
  dryRun,
  dryRunRow,
  serveFile,
  serveImports,
  templateItem,
  templateList,
} from '../testFixtures';

const CHECK = `${BASE}/check?template=${TEMPLATE_ID}`;

function serveRun(report = dryRun()) {
  server.use(
    http.post(apiUrl(`${IMPORTS_URL}/templates/:id/dry-runs`), () => HttpResponse.json(report)),
    http.get(apiUrl(`${IMPORTS_URL}/dry-runs/:id`), () => HttpResponse.json(report)),
  );
}

async function runCheck() {
  await screen.findByRole('button', { name: texts.run });
  const input = document.querySelector('input[type="file"]') as HTMLInputElement;
  await userEvent.upload(input, new File(['x'], 'tasks.xlsx'));
  await waitFor(() => expect(screen.getByRole('button', { name: texts.run })).toBeEnabled());
  await userEvent.click(screen.getByRole('button', { name: texts.run }));
}

function refuse(reason: string, params: Record<string, string> = {}) {
  const base = problem('imports.file.refused');
  server.use(
    http.post(apiUrl(`${IMPORTS_URL}/templates/:id/dry-runs`), () =>
      HttpResponse.json(
        { ...base, errors: { file: [{ code: reason, detail: problemText(reason, params) }] } },
        { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
      ),
    ),
  );
}

describe('the file check: refusals of the file against the template', () => {
  it('names the missing columns of the template', async () => {
    serveImports();
    serveFile();
    refuse('imports.file.columns_missing', { columns: 'Статус, Срок' });
    renderPage(CHECK);
    await runCheck();
    expect(await screen.findByText(texts.fileRefused, { exact: false })).toBeInTheDocument();
    expect(
      screen.getByText(new RegExp('В файле нет столбцов, которые указаны в шаблоне: Статус, Срок')),
    ).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: texts.previewTable })).toBeNull();
  });

  it('names the missing sheet of the template', async () => {
    serveImports();
    serveFile();
    refuse('imports.file.sheet_missing', { sheet: 'Задачи' });
    renderPage(CHECK);
    await runCheck();
    expect(
      await screen.findByText(/В файле нет листа «Задачи», указанного в шаблоне\./),
    ).toBeInTheDocument();
  });

  it('shows a refusal of the file itself (too many rows) with its text', async () => {
    serveImports();
    serveFile();
    refuse('imports.file.too_many_rows');
    renderPage(CHECK);
    await runCheck();
    expect(await screen.findByText(/В файле больше 5 000 строк данных/)).toBeInTheDocument();
  });
});

describe('the file check: the report', () => {
  it('warns that the template changed after the check', async () => {
    serveImports();
    serveFile();
    serveRun(dryRun({ stale: true, importable: false }));
    renderPage(CHECK);
    await runCheck();
    expect(await screen.findByText(texts.stale)).toBeInTheDocument();
  });

  it.skip('does not claim «nothing to import» for a stale report whose rows are fine (QA-FD-4)', async () => {
    serveImports();
    serveFile();
    serveRun(dryRun({ stale: true, importable: false }));
    renderPage(CHECK);
    await runCheck();
    await screen.findByText(texts.stale);
    expect(screen.queryByText(texts.nothingToImport)).toBeNull();
  });

  it('says there is nothing to import when every row is an error', async () => {
    serveImports();
    serveFile();
    serveRun(
      dryRun({
        importable: false,
        totals: { created: 0, updated: 0, unchanged: 0, skipped: 0, error: 2, all: 2 },
      }),
    );
    renderPage(CHECK);
    await runCheck();
    expect(await screen.findByText(texts.nothingToImport)).toBeInTheDocument();
  });

  it('says the file has no data rows', async () => {
    serveImports();
    serveFile();
    serveRun(
      dryRun({
        importable: false,
        notes: ['imports.dry_run.empty_file'],
        totals: { created: 0, updated: 0, unchanged: 0, skipped: 0, error: 0, all: 0 },
        rows: [],
        rowsShown: 0,
      }),
    );
    renderPage(CHECK);
    await runCheck();
    expect(await screen.findByText(problemText('imports.dry_run.empty_file'))).toBeInTheDocument();
  });

  it('shows the totals of the whole file on every result button', async () => {
    serveImports();
    serveFile();
    serveRun(
      dryRun({
        totals: { created: 120, updated: 30, unchanged: 40, skipped: 2, error: 8, all: 200 },
      }),
    );
    renderPage(CHECK);
    await runCheck();
    for (const [name, count] of [
      ['created', 120],
      ['updated', 30],
      ['unchanged', 40],
      ['skipped', 2],
      ['error', 8],
    ] as const) {
      expect(
        await screen.findByRole('button', { name: `${texts.results[name]}: ${count}` }),
      ).toBeInTheDocument();
    }
  });

  it('says a filter with no rows', async () => {
    serveImports();
    serveFile();
    serveRun(dryRun());
    server.use(
      http.get(apiUrl(`${IMPORTS_URL}/dry-runs/:id`), ({ request }) => {
        const result = new URL(request.url).searchParams.get('result');
        return HttpResponse.json(
          result === 'skipped' ? dryRun({ result: 'skipped', rows: [], rowsShown: 0 }) : dryRun(),
        );
      }),
    );
    renderPage(CHECK);
    await runCheck();
    await userEvent.click(
      await screen.findByRole('button', { name: `${texts.results.skipped}: 0` }),
    );
    expect(await screen.findByText(texts.noRowsOfResult)).toBeInTheDocument();
  });

  it('shows old and new values of an update in the row detail and keeps no placeholder import control', async () => {
    serveImports();
    serveFile();
    serveRun(
      dryRun({
        rows: [
          dryRunRow({
            row: 5,
            result: 'updated',
            item: { title: 'Задача', identifier: 'PLT-5' },
            fields: [{ field: 'title', name: 'Название', value: 'Новое', was: 'Старое' }],
          }),
        ],
        rowsShown: 1,
      }),
    );
    renderPage(CHECK);
    await runCheck();
    await userEvent.click(await screen.findByRole('button', { name: texts.expandRow(5) }));
    expect(
      await screen.findByText(`${texts.was}: Старое → ${texts.became}: Новое`),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Импортировать/ })).toBeNull();
  });

  it('offers only the templates the user may run', async () => {
    serveImports({
      list: templateList(
        [
          templateItem({ canRun: true }),
          templateItem({
            id: 'e0000000-0000-4000-8000-0000000000ff',
            name: 'Чужой',
            canRun: false,
          }),
        ],
        { canRun: true },
      ),
    });
    renderPage(`${BASE}/check`);
    const select = await screen.findByRole('combobox', { name: texts.template });
    await userEvent.click(select);
    const options = (await screen.findAllByRole('option')).map((option) => option.textContent);
    expect(options).toContain('Выгрузка задач');
    expect(options).not.toContain('Чужой');
  });

  it('says a download of the report failed', async () => {
    serveImports();
    serveFile();
    serveRun();
    server.use(
      http.get(apiUrl(`${IMPORTS_URL}/dry-runs/:id/report`), () =>
        HttpResponse.json(problem('imports.dry_run.not_found'), {
          status: 404,
          headers: { 'Content-Type': 'application/problem+json' },
        }),
      ),
    );
    renderPage(CHECK);
    await runCheck();
    await userEvent.click(await screen.findByRole('button', { name: texts.downloadReport }));
    expect(await screen.findByText(new RegExp(texts.reportFailed))).toBeInTheDocument();
    expect(
      within(document.body).getAllByText(new RegExp(problemText('imports.dry_run.not_found')))
        .length,
    ).toBeGreaterThan(0);
  });
});
