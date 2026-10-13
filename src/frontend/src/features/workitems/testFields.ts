// Helpers of the form's tests over feat-022's fields per type: opening a
// form on a dataset, reading its sections and their controls the way a user
// meets them, picking from a fixed list, and the saves the API received.
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { serveWorkItems, type RecordedRequest, type WorkItemsTestApi } from './testApi';
import { hierarchyDataset, type Dataset } from './testFixtures';
import { mskForm, renderWorkItems } from './testRender';

/**
 * Opens «Мастаск»'s `identifier` on feat-001's dataset by default: MSK-EPIC-1,
 * MSK-US-2, MSK-TASK-3, MSK-BUG-4, MSK-FEAT-6 — one item of every type.
 */
export async function openFieldsForm(identifier: string, data: Dataset = hierarchyDataset()) {
  const api = serveWorkItems(data);
  const view = renderWorkItems(mskForm(identifier));
  await screen.findByRole('textbox', { name: 'Заголовок' });
  return { api, view };
}

/** The identifier of each type's item in `hierarchyDataset()`. */
export const ITEM_OF_TYPE = {
  Эпик: 'MSK-EPIC-1',
  Фича: 'MSK-FEAT-6',
  'Пользовательская история': 'MSK-US-2',
  Задача: 'MSK-TASK-3',
  Баг: 'MSK-BUG-4',
} as const;

export const save = () => screen.getByRole('button', { name: 'Сохранить' });

/** A section of the form by its heading. */
export const section = (name: string) => screen.getByRole('region', { name });

/** A section's heading text. */
export function sectionName(region: HTMLElement): string {
  return region.querySelector('h2')?.textContent ?? '';
}

/** Every section of the form, in document — reading and Tab — order. */
export function sectionNames(): string[] {
  return screen.getAllByRole('region').map(sectionName);
}

/** The sections of the grid's column holding the section `name`, top to bottom. */
export function columnOf(name: string): string[] {
  const column = section(name).parentElement as HTMLElement;
  return within(column).getAllByRole('region').map(sectionName);
}

/**
 * The labels of a section's controls, top to bottom: the kit draws every field's
 * label as a real `label` element, a list's included.
 */
export function controlsOf(name: string): string[] {
  return Array.from(section(name).querySelectorAll('label')).map(
    (label) => label.textContent ?? '',
  );
}

/** A number field or a text by its label. */
export const textbox = (name: string) => screen.getByRole('textbox', { name });

/** A fixed list by its label. */
export const list = (name: string) => screen.getByRole('combobox', { name });

/** A date field by its label: the browser's date field has no role of its own. */
export const dateField = (name: string) => screen.getByLabelText<HTMLInputElement>(name);

/** The options a fixed list offers, in order. */
export async function optionsOf(name: string): Promise<string[]> {
  await userEvent.click(list(name));
  const options = within(await screen.findByRole('listbox'))
    .getAllByRole('option')
    .map((option) => option.textContent ?? '');
  await userEvent.keyboard('{Escape}');
  return options;
}

/** Picks `option` from the fixed list `name`. */
export async function choose(name: string, option: string): Promise<void> {
  await userEvent.click(list(name));
  await userEvent.click(
    within(await screen.findByRole('listbox')).getByRole('option', { name: option }),
  );
}

/** Replaces a text field's text, as a user selecting it all and typing. */
export async function retype(name: string, text: string): Promise<void> {
  await userEvent.clear(textbox(name));
  if (text !== '') {
    await userEvent.type(textbox(name), text);
  }
}

/** The PATCHes the API received. */
export function patches(api: WorkItemsTestApi): RecordedRequest[] {
  return api.writes().filter((request) => request.method === 'PATCH');
}
