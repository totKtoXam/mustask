import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Backlog } from '../api/boardsApi';
import { teamScope } from '../lib/scope';
import { ViewFilterRow } from './ViewFilterRow';

// feat-040 design § 4.2: the states of a team's project filter row — before the first read, after a
// failed one, for a team on no project, with a value the team does not know, and over a re-read.

const LEVEL = { code: 'stories', name: 'Истории', types: [] };
const ALPHA = { id: 'p-alpha', name: 'Alpha' };
const BETA = { id: 'p-beta', name: 'Beta' };

function read(projects: Backlog['projects']): Backlog {
  return { team: { id: 't1', name: 'MARS' }, projects, level: LEVEL, items: [] };
}

function mount(props: {
  data?: Backlog;
  failed?: boolean;
  filterIds?: readonly string[];
  onChange?: (values: string[]) => void;
  refetch?: () => void;
}) {
  const view = render(
    <ViewFilterRow
      organizationId="o1"
      scope={teamScope('t1')}
      filterIds={props.filterIds ?? []}
      read={{
        data: props.data,
        failed: props.failed ?? false,
        refetch: props.refetch ?? (() => {}),
      }}
      onChange={props.onChange ?? (() => {})}
    />,
  );
  return view;
}

function field() {
  return screen.getByRole('combobox', { name: 'Проект' });
}

describe('feat-040 design § 4.2 the project filter row', () => {
  it('is drawn at once, before the first read, and its list says «Загрузка…»', async () => {
    mount({});

    expect(screen.getByRole('group', { name: 'Фильтры' })).toBeInTheDocument();
    expect(screen.getByText('Все проекты')).toBeInTheDocument();
    await userEvent.click(field());
    expect(within(await screen.findByRole('listbox')).getByText('Загрузка…')).toBeVisible();
  });

  it('stays after a failed first read, its list saying so; opening it reads the page again', async () => {
    const refetch = vi.fn();
    mount({ failed: true, refetch });

    await userEvent.click(field());

    expect(
      within(await screen.findByRole('listbox')).getByText(
        'Не удалось загрузить проекты. Попробуйте ещё раз.',
      ),
    ).toBeVisible();
    expect(refetch).toHaveBeenCalledTimes(1);
  });

  it('offers the team’s projects by name, and reports the choice', async () => {
    const onChange = vi.fn();
    mount({ data: read([ALPHA, BETA]), onChange });

    await userEvent.click(field());
    const options = within(await screen.findByRole('listbox')).getAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual(['Alpha', 'Beta']);
    await userEvent.click(options[1] as HTMLElement);

    expect(onChange).toHaveBeenCalledWith(['p-beta']);
  });

  it('names the projects chosen, joined, and offers to clear them', async () => {
    const onChange = vi.fn();
    mount({ data: read([ALPHA, BETA]), filterIds: ['p-beta', 'p-alpha'], onChange });

    expect(field()).toHaveTextContent('Alpha, Beta');
    await userEvent.click(screen.getByRole('button', { name: 'Очистить фильтры' }));

    expect(onChange).toHaveBeenCalledWith([]);
  });

  it('is not drawn for a team on no project when nothing is filtered: there is nothing to filter by', () => {
    mount({ data: read([]) });

    expect(screen.queryByRole('group', { name: 'Фильтры' })).toBeNull();
  });

  it('is drawn for a team on no project when the address carries a value: «Фильтр задан», no project listed, and a way to clear it', async () => {
    mount({ data: read([]), filterIds: ['p-gone'] });

    expect(field()).not.toHaveTextContent('Все проекты');
    expect(screen.getByText('Фильтр задан')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Очистить фильтры' })).toBeVisible();
    await userEvent.click(field());
    expect(within(await screen.findByRole('listbox')).getByText('Нет проектов')).toBeVisible();
  });

  it('reads «Фильтр задан» for values that name none of the team’s projects, and the names of the known ones beside them', () => {
    const { rerender } = mount({ data: read([ALPHA, BETA]), filterIds: ['p-gone'] });
    expect(screen.getByText('Фильтр задан')).toBeInTheDocument();

    rerender(
      <ViewFilterRow
        organizationId="o1"
        scope={teamScope('t1')}
        filterIds={['p-gone', 'p-beta']}
        read={{ data: read([ALPHA, BETA]), failed: false, refetch: () => {} }}
        onChange={() => {}}
      />,
    );

    expect(field()).toHaveTextContent('Beta');
    expect(screen.queryByText('Фильтр задан')).toBeNull();
  });

  it('keeps offering the last projects read while the view is read again, and after that read fails', async () => {
    const { rerender } = mount({ data: read([ALPHA, BETA]) });
    const again = (data: Backlog | undefined, failed: boolean) =>
      rerender(
        <ViewFilterRow
          organizationId="o1"
          scope={teamScope('t1')}
          filterIds={['p-alpha']}
          read={{ data, failed, refetch: () => {} }}
          onChange={() => {}}
        />,
      );

    again(undefined, false);
    await userEvent.click(field());
    expect(
      within(await screen.findByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Alpha', 'Beta']);
    await userEvent.keyboard('{Escape}');

    again(undefined, true);
    await userEvent.click(field());
    expect(
      within(await screen.findByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Alpha', 'Beta']);
    expect(screen.queryByText('Не удалось загрузить проекты. Попробуйте ещё раз.')).toBeNull();
  });
});
