import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import { MAIN_REGION_ID } from '../../../shared/lib/mainRegion';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { apiUrl, recordRequests } from '../../../test/api';
import { server } from '../../../test/mocks/server';
import { renderPage } from '../../../test/renderPage';
import { PROCESS_DEFINITION_URL, sampleProcessDefinition } from '../../process/testFixtures';
import { texts } from '../lib/texts';
import {
  BASE,
  IMPORTS_URL,
  TEMPLATE_ID,
  catalogue,
  fileRead,
  serveFile,
  serveImports,
  template,
} from '../testFixtures';

const SAVED = `${BASE}/templates/${TEMPLATE_ID}`;

function serveReferences() {
  server.use(
    http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(sampleProcessDefinition)),
    http.get(apiUrl('/api/organizations/:organizationId/members'), () => HttpResponse.json([])),
  );
}

async function pickSample() {
  const input = await waitFor(() => {
    const found = document.querySelector('input[type="file"]');
    if (!found) throw new Error('no file input yet');
    return found as HTMLInputElement;
  });
  await userEvent.upload(input, new File(['x'], 'tasks.xlsx'));
  await screen.findByText(/tasks\.xlsx · /);
}

describe('the editor: «Проверить файл»', () => {
  it('is a link to the check of this template for a saved, unchanged template', async () => {
    serveImports();
    renderPage(SAVED);
    await screen.findByDisplayValue('Выгрузка задач');
    const link = await screen.findByRole('link', { name: texts.checkFile });
    expect(link).toHaveAttribute('href', expect.stringContaining(`template=${TEMPLATE_ID}`));
  });

  it('is disabled as soon as the draft differs from the saved template', async () => {
    serveImports();
    renderPage(SAVED);
    const name = await screen.findByDisplayValue('Выгрузка задач');
    await userEvent.type(name, ' 2');
    expect(screen.queryByRole('link', { name: texts.checkFile })).toBeNull();
    expect(screen.getByRole('button', { name: texts.checkFile })).toBeDisabled();
    expect(screen.getByText(texts.saveFirst)).toBeInTheDocument();
  });

  it('is not offered on an unsaved template', async () => {
    serveImports();
    renderPage(`${BASE}/templates/new`);
    await screen.findByRole('tab', { name: new RegExp(texts.tabs.source) });
    expect(screen.queryByRole('link', { name: texts.checkFile })).toBeNull();
    expect(screen.queryByRole('button', { name: texts.checkFile })).toBeNull();
  });

  it('is not offered to who may not run a check', async () => {
    serveImports({ template: template({ canRun: false }) });
    renderPage(SAVED);
    await screen.findByDisplayValue('Выгрузка задач');
    expect(screen.queryByRole('link', { name: texts.checkFile })).toBeNull();
    expect(screen.queryByRole('button', { name: texts.checkFile })).toBeNull();
  });
});

describe('the editor: rights from the served flags', () => {
  it('offers copy and delete only by canCopy and canDelete', async () => {
    serveImports({ template: template({ canDelete: false, canCopy: true }) });
    renderPage(SAVED);
    await screen.findByDisplayValue('Выгрузка задач');
    expect(await screen.findByRole('button', { name: texts.copyCommand })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: texts.deleteCommand })).toBeNull();
    expect(screen.queryByRole('menuitem', { name: texts.deleteCommand })).toBeNull();
  });

  it('a read-only template still offers the check and the copy it is allowed', async () => {
    serveImports({ template: template({ canEdit: false, canDelete: false, canCopy: true }) });
    renderPage(SAVED);
    await screen.findByDisplayValue('Выгрузка задач');
    expect(screen.queryByRole('button', { name: texts.save })).toBeNull();
    expect(await screen.findByRole('link', { name: texts.checkFile })).toBeInTheDocument();
  });
});

describe('the editor: out-of-scope features are absent', () => {
  it('draws no import, export, link or participant control', async () => {
    serveImports();
    serveReferences();
    renderPage(SAVED);
    await screen.findByDisplayValue('Выгрузка задач');
    // the shell's navigation may carry its own entries; the page is what is checked
    const page = within(document.getElementById(MAIN_REGION_ID) ?? document.body);
    for (const name of [/Импортировать/, /Экспорт/, /Внешние ссылки/, /Участники/, /скоро/i]) {
      expect(page.queryByRole('button', { name })).toBeNull();
      expect(page.queryByRole('link', { name })).toBeNull();
    }
  });
});

describe('the editor: the sample file across a save', () => {
  // QA-FD-2 (open): the save refreshes the template query, the editor is keyed by its version and
  // remounts, so the sample file held in the editor's state is lost right after «Сохранить».
  it('keeps the sample file after a save (QA-FD-2)', async () => {
    serveImports();
    serveFile(
      fileRead({
        columns: [
          { index: 0, header: 'Название', address: 'A1', proposedField: 'title' },
          { index: 1, header: 'Комментарий', address: 'B1', proposedField: null },
        ],
      }),
    );
    recordRequests('put', `${IMPORTS_URL}/templates/:id`, () => template({ version: 2 }));
    renderPage(SAVED);
    const name = await screen.findByDisplayValue('Выгрузка задач');
    await pickSample();
    await userEvent.type(name, ' 2');
    await userEvent.click(screen.getByRole('button', { name: texts.save }));
    expect(await screen.findByText(texts.saved)).toBeInTheDocument();
    expect(screen.getByText(/tasks\.xlsx · /)).toBeInTheDocument();
    expect(screen.queryByText(texts.sampleExpired)).toBeNull();
  });
});

describe('the editor: the held sample does not leak (QA re-check 1)', () => {
  // heldSample is module state: an abandoned new template must not leave its sample for the next
  // unrelated new template (QA-FD-9, fixed: a new editor drops the `new` key on mount).
  it('does not offer the sample of an abandoned new template to the next new one (QA-FD-9)', async () => {
    serveImports();
    serveFile();
    const first = renderPage(`${BASE}/templates/new`);
    await pickSample();
    first.unmount();
    renderPage(`${BASE}/templates/new`);
    await waitFor(() => expect(document.querySelector('input[type="file"]')).not.toBeNull());
    expect(screen.queryByText(/tasks\.xlsx · /)).toBeNull();
  });
});

describe('the chain dialog', () => {
  const withLookup = {
    ...catalogue,
    converters: [
      ...catalogue.converters,
      {
        id: 'lookup',
        name: 'По таблице',
        class: 'lookup',
        isSource: false,
        params: [],
      },
    ],
  };

  async function openChain() {
    serveImports();
    serveReferences();
    server.use(http.get(apiUrl(`${IMPORTS_URL}/catalogue`), () => HttpResponse.json(withLookup)));
    renderPage(SAVED);
    await userEvent.click(await screen.findByRole('tab', { name: new RegExp(texts.tabs.mapping) }));
    await screen.findByRole('table', { name: texts.mappingTable });
    await userEvent.click(
      screen.getByRole('button', { name: `${texts.mappingColumns.chain}: Название` }),
    );
    await screen.findByText(texts.chainEmpty);
  }

  it('adds a converter, shows it in the list and applies it to the draft only', async () => {
    const { requests } = recordRequests('put', `${IMPORTS_URL}/templates/:id`);
    await openChain();
    await userEvent.click(screen.getByRole('button', { name: texts.addConverter }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Обрезать пробелы' }));
    expect(await screen.findByText('1. Обрезать пробелы')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: texts.apply }));
    await waitFor(() => expect(screen.queryByText(texts.chainEmpty)).toBeNull());
    expect(requests).toHaveLength(0);
    expect(screen.getByText('Обрезать пробелы')).toBeInTheDocument();
  });

  it('cancel leaves the chain of the mapping as it was', async () => {
    await openChain();
    await userEvent.click(screen.getByRole('button', { name: texts.addConverter }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Обрезать пробелы' }));
    await userEvent.click(screen.getByRole('button', { name: texts.cancel }));
    await waitFor(() => expect(screen.queryByText('1. Обрезать пробелы')).toBeNull());
    expect(
      screen.getByRole('button', { name: `${texts.mappingColumns.chain}: Название` }),
    ).toHaveTextContent(texts.addChain);
  });

  it('removes a step', async () => {
    await openChain();
    await userEvent.click(screen.getByRole('button', { name: texts.addConverter }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Обрезать пробелы' }));
    await userEvent.click(
      screen.getByRole('button', { name: `${texts.removeStep}: Обрезать пробелы 1` }),
    );
    expect(await screen.findByText(texts.chainEmpty)).toBeInTheDocument();
  });

  // QA-FD-3 (open): the third policy of «По таблице» is sent as `keep`; the API takes only
  // error | default | empty (api-contract § 2.1) and refuses the template with converter_param_invalid.
  it('sends only a policy the API accepts for «По таблице» (QA-FD-3)', async () => {
    const sent: unknown[] = [];
    server.use(
      http.put(apiUrl(`${IMPORTS_URL}/templates/:id`), async ({ request }) => {
        sent.push(await request.json());
        return HttpResponse.json(template({ version: 2 }));
      }),
    );
    await openChain();
    await userEvent.click(screen.getByRole('button', { name: texts.addConverter }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'По таблице' }));
    await userEvent.click(await screen.findByRole('combobox', { name: texts.lookupPolicy }));
    await userEvent.click(await screen.findByRole('option', { name: texts.lookupPolicyEmpty }));
    await userEvent.click(screen.getByRole('button', { name: texts.lookupAdd }));
    await userEvent.click(screen.getByRole('button', { name: texts.apply }));
    await userEvent.click(screen.getByRole('button', { name: texts.save }));
    await waitFor(() => expect(sent).toHaveLength(1));
    const policy = JSON.stringify(sent[0]).match(/"policy":"(\w+)"/)?.[1];
    expect(['error', 'default', 'empty']).toContain(policy);
  });
});
