// Driving the project filter of a team's views as a test sees it (feat-040 BR-TS-8): the filter row's
// group, its field «Проект», a choice of projects by name, and the router's address, which holds
// the choice.
import { expect } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

/** The router's pathname and search, which the shell’s location probe keeps. */
export function here(): { pathname: string; search: string } {
  const probe = screen.getByTestId('location');
  return { pathname: probe.textContent ?? '', search: probe.getAttribute('data-search') ?? '' };
}

/** The filter row, a group named «Фильтры». */
export function filterBand(): HTMLElement {
  return screen.getByRole('group', { name: 'Фильтры' });
}

/** «Проект» of a team's filter row. */
export function projectField(): HTMLElement {
  return within(filterBand()).getByRole('combobox', { name: 'Проект' });
}

/** The names of the options the open field lists, in order. */
export async function projectOptions(): Promise<string[]> {
  await userEvent.click(projectField());
  const names = within(await screen.findByRole('listbox'))
    .getAllByRole('option')
    .map((option) => option.textContent ?? '');
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
  return names;
}

/** Ticks the projects by name, each in the open list, and closes it with Escape. */
export async function chooseProjects(...names: string[]): Promise<void> {
  await userEvent.click(projectField());
  for (const name of names) {
    await userEvent.click(await screen.findByRole('option', { name }));
  }
  await userEvent.keyboard('{Escape}');
  await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
}
