import { afterEach, describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import { focusManager } from '@tanstack/react-query';
import { serveBoards } from '../testApi';
import { findBacklogTable } from '../testBacklog';
import { ids, projectDataset } from '../testFixtures';
import { projectBacklogAddress, renderBoards } from '../testRender';

// feat-043 QA, US-4 Scenario «A project's backlog under a chosen team keeps its order and cannot be
// reordered» (BR-NV-17, Q-9 A; design §§ 4, 7.1). Defect F-2 in qa.md, fixed: the hint and the
// «сужено» tag are drawn and the row menu offers no move.

afterEach(() => {
  focusManager.setFocused(undefined);
});

describe('feat-043 QA, US-4: a project’s backlog narrowed by a chosen team', () => {
  it('says that the order does not change while narrowed, and names the narrowing', async () => {
    serveBoards(projectDataset());
    renderBoards(`${projectBacklogAddress()}?teamId=${ids.platformTeam}`);
    await findBacklogTable();

    expect(
      screen.getByText(
        'Пока выбор сужает список, порядок не меняется. Выберите «все», чтобы менять его.',
      ),
    ).toBeVisible();
    expect(screen.getByText('сужено: команда Платформа')).toBeVisible();
  });
});
