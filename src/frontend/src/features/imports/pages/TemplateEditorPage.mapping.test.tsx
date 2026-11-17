import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { apiUrl } from '../../../test/api';
import { server } from '../../../test/mocks/server';
import { renderPage } from '../../../test/renderPage';
import { PROCESS_DEFINITION_URL, sampleProcessDefinition } from '../../process/testFixtures';
import { texts } from '../lib/texts';
import { BASE, TEMPLATE_ID, serveImports } from '../testFixtures';

const SAVED = `${BASE}/templates/${TEMPLATE_ID}`;

async function openMapping() {
  serveImports();
  server.use(
    http.get(PROCESS_DEFINITION_URL, () => HttpResponse.json(sampleProcessDefinition)),
    http.get(apiUrl('/api/organizations/:organizationId/members'), () => HttpResponse.json([])),
  );
  renderPage(SAVED);
  await userEvent.click(await screen.findByRole('tab', { name: new RegExp(texts.tabs.mapping) }));
}

describe('the mapping tab', () => {
  it('lists the columns of the sample with the field each feeds', async () => {
    await openMapping();
    const table = await screen.findByRole('table', { name: texts.mappingTable });
    expect(within(table).getAllByRole('row').length).toBeGreaterThan(2);
    expect(within(table).getByText('Название')).toBeInTheDocument();
  });

  it('opens the chain of a mapping in its dialog', async () => {
    await openMapping();
    await screen.findByRole('table', { name: texts.mappingTable });
    await userEvent.click(
      screen.getByRole('button', { name: `${texts.mappingColumns.chain}: Название` }),
    );
    expect(await screen.findByText(texts.chainEmpty)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: texts.apply })).toBeInTheDocument();
  });
});
