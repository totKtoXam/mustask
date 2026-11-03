import { afterEach, describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import { focusManager } from '@tanstack/react-query';
import { serveBoards } from '../testApi';
import { findBacklogTable } from '../testBacklog';
import { ids, projectDataset } from '../testFixtures';
import { projectBacklogAddress, renderBoards } from '../testRender';
import { narrowedTag } from '../lib/narrowed';

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

// Round 2 probes: what a narrowing is, and is not.
const HINT = 'Пока выбор сужает список, порядок не меняется. Выберите «все», чтобы менять его.';

describe('feat-043 QA round 2, US-4: the edges of a narrowed project backlog', () => {
  it('draws no drag handle on a narrowed backlog (design § 7.1, US-4)', async () => {
    serveBoards(projectDataset());
    renderBoards(`${projectBacklogAddress()}?teamId=${ids.platformTeam}`);
    const table = await findBacklogTable();

    expect(table.querySelectorAll('[data-grip]')).toHaveLength(0);
  });

  it.each([
    ['another dimension, the project filter', `?projectId=${ids.msk}`],
    ['the department selector', `?departmentId=${ids.msk}`],
    ['nothing', ''],
  ])('is not narrowed by %s', async (_name, query) => {
    serveBoards(projectDataset());
    renderBoards(`${projectBacklogAddress()}${query}`);
    const table = await findBacklogTable();

    expect(screen.queryByText(HINT)).not.toBeInTheDocument();
    expect(screen.queryByText(/^сужено/)).not.toBeInTheDocument();
    expect(table.querySelectorAll('[data-grip]').length).toBeGreaterThan(0);
  });

  // Round 2 F-6 (fixed): the tag for the no-team filter alone reads «сужено: команда без команды».
  it('names «без команды» for the no-team filter alone, not «команда без команды»', () => {
    expect(narrowedTag(['none'], [])).toBe('сужено: без команды');
  });
});
