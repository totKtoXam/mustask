import { afterEach, describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { PROBLEM_HEADERS } from '../testFixtures';
import { ORG, header, orgAddress, renderAggregate, serveAggregate } from '../testAggregate';

// feat-061 US-6 (closes feat-041 R-4): a header selection naming an object the caller cannot see gets
// the object's not-found code from the server; the aggregate views answer with its not-found frame,
// not with the generic error whose «Повторить» can never succeed.

afterEach(() => localStorage.clear());

const GONE = () =>
  HttpResponse.json(
    {
      status: 404,
      title: 'organizations.area.not_found',
      errorCode: 'organizations.area.not_found',
    },
    { status: 404, headers: PROBLEM_HEADERS },
  );

describe('feat-061: a hidden selection on the aggregate views', () => {
  it('the backlog shows «Область не найдена» and no «Повторить» when the groups read is a 404', async () => {
    serveAggregate({
      groups: { project: [header('project', 'p-1', 'Мастаск', 1, { key: 'MSK' })] },
      fail: (request) => (request.kind === 'groups' ? GONE() : undefined),
    });
    renderAggregate(`/organizations/${ORG}/areas/d-hidden/backlogs/stories`);
    expect(await screen.findByText('Область не найдена')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Повторить' })).not.toBeInTheDocument();
  });

  it('the flat «Рабочие элементы» shows the frame when the list read is a 404', async () => {
    serveAggregate({
      groups: { none: [header('none', null, null, 0)] },
      list: { count: 0, data: [] },
      fail: (request) => (request.kind === 'list' ? GONE() : undefined),
    });
    renderAggregate(orgAddress('workitems') + '?departmentId=d-hidden');
    expect(await screen.findByText('Область не найдена')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Повторить' })).not.toBeInTheDocument();
  });

  it('the board shows the frame when the groups read is a 404', async () => {
    serveAggregate({
      groups: { project: [header('project', 'p-1', 'Мастаск', 1, { key: 'MSK' })] },
      fail: (request) => (request.kind === 'groups' ? GONE() : undefined),
    });
    renderAggregate(orgAddress('boards'));
    expect(await screen.findByText('Область не найдена')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Повторить' })).not.toBeInTheDocument();
  });

  it('keeps the generic error for any other failure', async () => {
    serveAggregate({
      groups: { project: [] },
      fail: (request) =>
        request.kind === 'groups' ? new HttpResponse(null, { status: 500 }) : undefined,
    });
    renderAggregate(orgAddress('backlogs'));
    expect(await screen.findByRole('button', { name: 'Повторить' })).toBeInTheDocument();
  });
});
