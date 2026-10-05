import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ORGANIZATIONS_URL, problem } from '../testFixtures';
import { serveOrganizations, serveProblem } from '../testApi';
import { CREATE_RIGHT, renderPage } from '../testRender';

async function openCreate() {
  const api = serveOrganizations();
  const rendered = renderPage({ path: '/organizations', permissions: CREATE_RIGHT });
  await screen.findByRole('table', { name: 'Активные организации' });
  await userEvent.click(screen.getByRole('button', { name: 'Создать организацию' }));
  const dialog = await screen.findByRole('dialog', { name: 'Новая организация' });
  return {
    ...rendered,
    api,
    dialog,
    field: within(dialog).getByRole('textbox', { name: 'Название' }),
  };
}

describe('mod-002 US-1 Scenario: An organization is created with its process', () => {
  it('opens «Новая организация», sends the name and opens the new organization’s «Проекты» with the notice', async () => {
    const { dialog, field } = await openCreate();
    expect(field).toHaveFocus();
    expect(within(dialog).getByText('От 1 до 100 символов')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Отмена' })).toBeInTheDocument();

    await userEvent.type(field, 'Пример организации');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await screen.findByText('Организация «Пример организации» создана.'),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByTestId('location').textContent).toMatch(
        /^\/organizations\/[^/]+\/settings\/projects$/,
      ),
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});

describe('mod-002 US-1 Scenario: The name is trimmed and its length checked', () => {
  it.each([[''], ['   '], ['я'.repeat(101)]])(
    'refuses %j on the field in the browser and sends nothing',
    async (value) => {
      const { api, dialog, field } = await openCreate();

      if (value) {
        await userEvent.type(field, value);
      }
      await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

      expect(
        within(dialog).getByText('Название должно содержать от 1 до 100 символов'),
      ).toBeInTheDocument();
      expect(field).toHaveAttribute('aria-invalid', 'true');
      expect(api.writes()).toEqual([]);
    },
  );

  it('clears the refusal as the field is edited', async () => {
    const { dialog, field } = await openCreate();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    await userEvent.type(field, 'А');

    expect(
      within(dialog).queryByText('Название должно содержать от 1 до 100 символов'),
    ).not.toBeInTheDocument();
    expect(within(dialog).getByText('От 1 до 100 символов')).toBeInTheDocument();
  });

  it('sends «  Пример организации  » as typed and names the stored, trimmed name in the notice', async () => {
    const { api, dialog, field } = await openCreate();

    await userEvent.type(field, '  Пример организации  ');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await screen.findByText('Организация «Пример организации» создана.'),
    ).toBeInTheDocument();
    expect(api.writes()[0]?.body).toEqual({ name: '  Пример организации  ' });
  });
});

describe('mod-002 US-1 Scenario: Unique instance-wide, deactivated included', () => {
  it('puts «Название «Гамма» уже занято» on the field, keeps the dialog open and the input', async () => {
    const { dialog, field } = await openCreate();

    await userEvent.type(field, 'гамма');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await within(dialog).findByText('Название «Гамма» уже занято')).toBeInTheDocument();
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveValue('гамма');
    expect(screen.getByRole('dialog', { name: 'Новая организация' })).toBeInTheDocument();
  });
});

describe('mod-002 US-1 Scenario: A failed seed leaves nothing behind', () => {
  it('shows «Не удалось создать организацию. Попробуйте ещё раз» in the dialog’s alert; a second submit succeeds', async () => {
    const { dialog, field } = await openCreate();
    serveProblem('post', ORGANIZATIONS_URL, problem('organizations.organization.create_failed'), {
      once: true,
    });

    await userEvent.type(field, 'Пример организации');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Не удалось создать организацию. Попробуйте ещё раз',
    );
    expect(field).toHaveValue('Пример организации');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(
      await screen.findByText('Организация «Пример организации» создана.'),
    ).toBeInTheDocument();
  });
});

describe('mod-002 US-1 Scenario: Without the right: neither offered nor accepted (a revoked right)', () => {
  it('a 403 create_forbidden with the right still in the token shows «Недостаточно прав» in the dialog’s alert', async () => {
    const { dialog, field } = await openCreate();
    serveProblem('post', ORGANIZATIONS_URL, problem('organizations.organization.create_forbidden'));

    await userEvent.type(field, 'Пример организации');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Создать' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Недостаточно прав');
  });
});

describe('mod-002 design § 4.3: the dialog closes with nothing sent', () => {
  it('«Отмена» closes it and returns focus to «Создать организацию»', async () => {
    const { api, dialog } = await openCreate();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Отмена' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Создать организацию' })).toHaveFocus();
    expect(api.writes()).toEqual([]);
  });
});
