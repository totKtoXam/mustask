import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { apiUrl, recordRequests } from '../../../test/api';
import { server } from '../../../test/mocks/server';
import { renderPage } from '../../../test/renderPage';
import { PROCESS_DEFINITION_URL, sampleProcessDefinition } from '../../process/testFixtures';
import type { Catalogue, CatalogueField, TemplateDto } from '../api/importsApi';
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
const STATE_MAPPING = 'm0000000-0000-4000-8000-0000000000a1';
const PRIORITY_MAPPING = 'm0000000-0000-4000-8000-0000000000a2';
const epic = sampleProcessDefinition.workItemTypes[0]!;
const newState = epic.states.find((state) => state.name === 'Новая')!;

function field(overrides: Partial<CatalogueField> & Pick<CatalogueField, 'id' | 'name'>) {
  return {
    group: 'main',
    required: false,
    multiValued: false,
    recoding: 'none',
    allowsEmptyPolicy: false,
    supportsClear: true,
    typeKinds: null,
    valueKind: 'reference',
    ...overrides,
  } as CatalogueField;
}

const recodedCatalogue: Catalogue = {
  ...catalogue,
  fields: [
    ...catalogue.fields,
    field({ id: 'state', name: 'Статус', recoding: 'state', allowsEmptyPolicy: true }),
    field({ id: 'priority', name: 'Приоритет', recoding: 'priority', allowsEmptyPolicy: false }),
  ],
};

function recodedTemplate(overrides: Partial<TemplateDto> = {}): TemplateDto {
  const base = template();
  return {
    ...base,
    settings: {
      ...base.settings,
      source: { ...base.settings.source, headers: ['Название', 'Статус', 'Приоритет'] },
      mappings: [
        ...base.settings.mappings,
        {
          id: STATE_MAPPING,
          field: 'state',
          source: { kind: 'column', column: 'Статус' },
          chain: [],
          emptyCell: 'keep',
          fieldAbsent: 'error',
        },
        {
          id: PRIORITY_MAPPING,
          field: 'priority',
          source: { kind: 'column', column: 'Приоритет' },
          chain: [],
          emptyCell: 'keep',
          fieldAbsent: 'error',
        },
      ],
      recoding: [
        { mappingId: STATE_MAPPING, policy: 'error', defaultValue: null, entries: [] },
        { mappingId: PRIORITY_MAPPING, policy: 'error', defaultValue: null, entries: [] },
      ],
      ignoredColumns: [],
    },
    ...overrides,
  };
}

function serve(options: { template?: TemplateDto; values?: object } = {}) {
  serveImports({ template: options.template ?? recodedTemplate() });
  server.use(
    http.get(apiUrl(`${IMPORTS_URL}/catalogue`), () => HttpResponse.json(recodedCatalogue)),
    http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(sampleProcessDefinition)),
    http.get(apiUrl('/api/organizations/:organizationId/members'), () => HttpResponse.json([])),
    http.post(apiUrl(`${IMPORTS_URL}/files/:id/values`), () =>
      HttpResponse.json(
        options.values ?? {
          values: [
            { value: 'Новая', rows: 3 },
            { value: 'Активна', rows: 2 },
            { value: 'Сдано', rows: 1 },
          ],
          distinctCount: 3,
          exceeded: false,
        },
      ),
    ),
  );
}

async function openValues(withFile: boolean) {
  renderPage(SAVED);
  await screen.findByDisplayValue('Выгрузка задач');
  if (withFile) {
    serveFile(
      fileRead({
        columns: [
          { index: 0, header: 'Название', address: 'A1', proposedField: 'title' },
          { index: 1, header: 'Статус', address: 'B1', proposedField: null },
          { index: 2, header: 'Приоритет', address: 'C1', proposedField: null },
        ],
      }),
    );
    const input = await waitFor(() => {
      const found = document.querySelector('input[type="file"]');
      if (!found) throw new Error('no file input yet');
      return found as HTMLInputElement;
    });
    await userEvent.upload(input, new File(['x'], 'tasks.xlsx'));
    await screen.findByText(/tasks\.xlsx · /);
  }
  await userEvent.click(await screen.findByRole('tab', { name: new RegExp(texts.tabs.values) }));
}

describe('the values tab', () => {
  it('says there is nothing to recode when no mapped field needs a table', async () => {
    serveImports();
    renderPage(SAVED);
    await screen.findByDisplayValue('Выгрузка задач');
    await userEvent.click(await screen.findByRole('tab', { name: new RegExp(texts.tabs.values) }));
    expect(await screen.findByText(texts.valuesEmptyHeading)).toBeInTheDocument();
  });

  it('has one section per recoded mapping and lists the values of the file with their row counts', async () => {
    serve();
    await openValues(true);
    expect(await screen.findByText(texts.valuesAccordion('Статус', 'Статус'))).toBeInTheDocument();
    expect(screen.getByText(texts.valuesAccordion('Приоритет', 'Приоритет'))).toBeInTheDocument();
    const table = await screen.findByRole('table', { name: texts.valuesTable('Статус') });
    expect(await within(table).findByText('Сдано')).toBeInTheDocument();
    expect(within(table).getByText('3')).toBeInTheDocument();
    expect(within(table).getAllByText(texts.statusNew).length).toBeGreaterThanOrEqual(3);
  });

  it('asks the server for the values of the mapping with the sample file', async () => {
    const seen: unknown[] = [];
    serve();
    server.use(
      http.post(apiUrl(`${IMPORTS_URL}/files/:id/values`), async ({ request }) => {
        seen.push(await request.json());
        return HttpResponse.json({ values: [], distinctCount: 0, exceeded: false });
      }),
    );
    await openValues(true);
    await waitFor(() => expect(seen.length).toBeGreaterThan(0));
    expect(seen[0]).toMatchObject({
      mapping: { id: STATE_MAPPING, field: 'state', source: { column: 'Статус' } },
    });
  });

  it('reads «—» for the counts and says why when no sample file is held', async () => {
    serve();
    await openValues(false);
    const table = await screen.findByRole('table', { name: texts.valuesTable('Статус') });
    expect(table).toBeInTheDocument();
    expect(screen.getAllByText(texts.noFileCounts).length).toBeGreaterThan(0);
  });

  it('suggests the value that is named the same and applies it only on acceptance', async () => {
    serve();
    await openValues(true);
    const table = await screen.findByRole('table', { name: texts.valuesTable('Статус') });
    expect(await within(table).findByText(texts.suggested('Активна'))).toBeInTheDocument();
    const target = within(table).getByRole('combobox', {
      name: `${texts.mustaskValue}: Активна`,
    });
    expect(target).not.toHaveTextContent('Активна');
    await userEvent.click(within(table).getByRole('button', { name: `${texts.accept}: Активна` }));
    await waitFor(() =>
      expect(
        within(table).getByRole('combobox', { name: `${texts.mustaskValue}: Активна` }),
      ).toHaveTextContent('Активна'),
    );
    expect(within(table).queryByText(texts.suggested('Активна'))).toBeNull();
  });

  it('accepts every suggestion at once', async () => {
    serve();
    await openValues(true);
    const table = await screen.findByRole('table', { name: texts.valuesTable('Статус') });
    await within(table).findByText(texts.suggested('Новая'));
    const buttons = screen.getAllByRole('button', { name: texts.acceptAll });
    await userEvent.click(buttons[0]!);
    await waitFor(() => expect(within(table).queryByText(texts.suggested('Новая'))).toBeNull());
    expect(within(table).queryByText(texts.suggested('Активна'))).toBeNull();
  });

  it('never offers the removed state as a target', async () => {
    serve();
    await openValues(true);
    const table = await screen.findByRole('table', { name: texts.valuesTable('Статус') });
    await userEvent.click(
      await within(table).findByRole('combobox', { name: `${texts.mustaskValue}: Сдано` }),
    );
    const names = (await screen.findAllByRole('option')).map((option) => option.textContent);
    expect(names).not.toContain('Удалена');
    expect(names).toContain('Закрыта');
  });

  it('offers «Оставить пустым» only for a field that may stay empty', async () => {
    serve();
    await openValues(false);
    await screen.findByRole('table', { name: texts.valuesTable('Статус') });
    const groups = screen.getAllByRole('radiogroup', { name: texts.policyLegend });
    expect(within(groups[0]!).getByLabelText(texts.policyEmpty)).toBeInTheDocument();
    expect(within(groups[1]!).queryByLabelText(texts.policyEmpty)).toBeNull();
  });

  it('refuses to save a default policy with no default value, on the values tab', async () => {
    serve();
    const { requests } = recordRequests('put', `${IMPORTS_URL}/templates/:id`, () =>
      recodedTemplate(),
    );
    await openValues(false);
    await screen.findByRole('table', { name: texts.valuesTable('Статус') });
    const groups = screen.getAllByRole('radiogroup', { name: texts.policyLegend });
    await userEvent.click(within(groups[0]!).getByLabelText(texts.policyDefault));
    await userEvent.click(screen.getByRole('button', { name: texts.save }));
    expect(await screen.findAllByText(texts.chooseValue)).not.toHaveLength(0);
    expect(requests).toHaveLength(0);
    expect(
      screen.getByRole('tab', { name: new RegExp(`${texts.tabs.values} \\(1\\)`) }),
    ).toBeInTheDocument();
  });

  it('marks a value whose saved target no longer exists as outdated', async () => {
    const stale = recodedTemplate();
    stale.settings.recoding[0] = {
      mappingId: STATE_MAPPING,
      policy: 'error',
      defaultValue: null,
      entries: [
        { fileValue: 'Новая', typeId: null, target: newState.id, onExport: false },
        { fileValue: 'Старая', typeId: null, target: 'gone-state', onExport: false },
      ],
    };
    serve({ template: stale });
    await openValues(false);
    const table = await screen.findByRole('table', { name: texts.valuesTable('Статус') });
    expect(await within(table).findByText(texts.statusOutdated)).toBeInTheDocument();
    expect(within(table).getAllByText(texts.statusOutdated)).toHaveLength(1);
  });

  it('adds an entry for one type under the row', async () => {
    serve();
    await openValues(true);
    const table = await screen.findByRole('table', { name: texts.valuesTable('Статус') });
    await userEvent.click(
      await within(table).findByRole('button', { name: `${texts.typeValue}: Новая` }),
    );
    expect(
      await within(table).findByRole('combobox', { name: `${texts.forType}: Новая` }),
    ).toBeInTheDocument();
  });

  it('replaces the table with a warning when the column is free text', async () => {
    serve({ values: { values: [], distinctCount: 1500, exceeded: true } });
    await openValues(true);
    expect(await screen.findAllByText(texts.tooManyValues('Статус'))).not.toHaveLength(0);
  });

  it('says the failure of reading the values and offers a retry', async () => {
    serve();
    server.use(
      http.post(apiUrl(`${IMPORTS_URL}/files/:id/values`), () =>
        HttpResponse.json(
          {
            status: 404,
            title: 'x',
            detail: 'Файл не найден.',
            errorCode: 'imports.file.not_found',
          },
          { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    await openValues(true);
    expect((await screen.findAllByText(texts.valuesFailed)).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('button', { name: texts.retry }).length).toBeGreaterThan(0);
  });

  it('is read-only for a template the user may not edit', async () => {
    serve({ template: recodedTemplate({ canEdit: false, canDelete: false }) });
    await openValues(false);
    await screen.findByRole('table', { name: texts.valuesTable('Статус') });
    expect(screen.queryByRole('button', { name: texts.acceptAll })).toBeNull();
    for (const radio of screen.getAllByRole('radio')) expect(radio).toBeDisabled();
    expect(screen.queryByRole('button', { name: `${texts.typeValue}: Новая` })).toBeNull();
  });
});
