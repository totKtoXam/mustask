import { describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { serveWorkItems, type WorkItemsTestApi } from '../testApi';
import { CANDIDATES_URL, exampleDataset, ids, type Dataset } from '../testFixtures';
import { held } from '../components/card/testCard';
import { mskForm, renderWorkItems } from '../testRender';

// Each user action draws the kit’s fields in jsdom; a test of several steps outlasts the default.
vi.setConfig({ testTimeout: 120_000 });

async function openForm(identifier: string, data: Dataset = exampleDataset()) {
  const api = serveWorkItems(data);
  renderWorkItems(mskForm(identifier));
  await screen.findByRole('textbox', { name: 'Заголовок' });
  return api;
}

function patches(api: WorkItemsTestApi) {
  return api.writes().filter((request) => request.method === 'PATCH');
}

const save = held(() => screen.getByRole('button', { name: 'Сохранить' }));
const assignee = () => screen.getByRole('combobox', { name: 'Исполнитель' });

/** The open list's lines, by their visible text without the avatars' initials. */
function lines(): string[] {
  return within(screen.getByRole('listbox'))
    .getAllByRole('option')
    .map((option) => {
      const name = option.querySelector('.MuiListItemText-primary');
      return (name ?? option).textContent ?? '';
    });
}

async function search(text: string) {
  await userEvent.click(assignee());
  await userEvent.clear(assignee());
  if (text) {
    await userEvent.paste(text);
  }
}

/** BUG-4 with no assignee: the stories' «Исполнитель «Не назначен»». */
function unassigned(): Dataset {
  const data = exampleDataset();
  const bug = data.items.find((record) => record.number === 4);
  if (bug) bug.assigneeId = null;
  return data;
}

describe('mod-003 US-4 Scenario: An assignee is found by name and saved', () => {
  it('offers «Не назначен», «Анна Кузнецова», «Анна Смирнова» with initials and e-mail; the pick is saved', async () => {
    const api = await openForm('MSK-BUG-4', unassigned());
    expect(assignee()).toHaveValue('');
    expect(screen.getByText('Не назначен')).toBeInTheDocument();

    await search('анна');

    await screen.findByRole('option', { name: /Анна Смирнова/ });
    expect(lines()).toEqual(['Не назначен', 'Анна Кузнецова', 'Анна Смирнова']);
    const smirnova = screen.getByRole('option', { name: /Анна Смирнова/ });
    expect(within(smirnova).getByText('АС')).toBeInTheDocument();
    expect(within(smirnova).getByText('anna.smirnova@example.com')).toBeInTheDocument();
    expect(api.reads('assignee-candidates').at(-1)?.path).toBe(
      `/api/organizations/${ids.example}/workitems/assignee-candidates?text=${encodeURIComponent('анна')}`,
    );

    await userEvent.click(smirnova);
    expect(assignee()).toHaveValue('Анна Смирнова');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api).map((request) => request.body)).toEqual([
      { assignee: { userId: ids.anna } },
    ]);
    expect(assignee()).toHaveValue('Анна Смирнова');

    await userEvent.click(
      within(screen.getByRole('navigation', { name: 'Путь к странице' })).getByRole('link', {
        name: 'Рабочие элементы',
      }),
    );
    const row = within(await screen.findByRole('table', { name: 'Рабочие элементы' }))
      .getAllByRole('row')
      .find((candidate) => candidate.textContent?.includes('MSK-BUG-4'));
    expect(row).toHaveTextContent('Анна Смирнова');
  });
});

describe('mod-003 US-4 Scenario: An assignee is found by name and saved — over an existing one', () => {
  it('replaces «Анна Смирнова» with «Анна Кузнецова» found by typing, the typed text kept while searching', async () => {
    const api = await openForm('MSK-BUG-4');
    expect(assignee()).toHaveValue('Анна Смирнова');

    await search('Кузнецова');

    expect(assignee()).toHaveValue('Кузнецова');
    await userEvent.click(await screen.findByRole('option', { name: /Анна Кузнецова/ }));
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api).map((request) => request.body)).toEqual([
      { assignee: { userId: ids.annaK } },
    ]);
    expect(assignee()).toHaveValue('Анна Кузнецова');
  });
});

describe('mod-003 US-4 Scenario: A user is found by e-mail', () => {
  it('offers «Анна Смирнова» for «smirnova@»', async () => {
    await openForm('MSK-BUG-4', unassigned());

    await search('smirnova@');

    expect(await screen.findByRole('option', { name: /Анна Смирнова/ })).toBeInTheDocument();
    expect(lines()).toEqual(['Не назначен', 'Анна Смирнова']);
  });
});

describe('mod-003 US-4 Scenario Outline: The picker’s hints', () => {
  it('«а»: «Не назначен» and «Введите не менее 2 символов имени или почты», nothing asked', async () => {
    const api = await openForm('MSK-BUG-4', unassigned());

    await search('а');

    expect(lines()).toEqual(['Не назначен', 'Введите не менее 2 символов имени или почты']);
    expect(
      screen.getByRole('option', { name: 'Введите не менее 2 символов имени или почты' }),
    ).toHaveAttribute('aria-disabled', 'true');
    expect(api.reads('assignee-candidates')).toEqual([]);
  });

  it('«Ёжиков» (nobody matches): «Не назначен» and «Никого не найдено»', async () => {
    await openForm('MSK-BUG-4', unassigned());

    await search('Ёжиков');

    expect(await screen.findByRole('option', { name: 'Никого не найдено' })).toHaveAttribute(
      'aria-disabled',
      'true',
    );
    expect(lines()).toEqual(['Не назначен', 'Никого не найдено']);
  });

  it('a text 25 active users match: «Не назначен», 20 users, «Показаны первые 20. Уточните запрос.»', async () => {
    const data = unassigned();
    for (let index = 10; index < 35; index += 1) {
      data.users.push({
        id: `user-${index}`,
        displayName: `Тестовый пользователь ${index}`,
        email: `test${index}@example.com`,
        active: true,
      });
    }
    await openForm('MSK-BUG-4', data);

    await search('Тестовый');

    expect(
      await screen.findByRole('option', { name: 'Показаны первые 20. Уточните запрос.' }),
    ).toHaveAttribute('aria-disabled', 'true');
    const shown = lines();
    expect(shown).toHaveLength(22);
    expect(shown[0]).toBe('Не назначен');
    expect(shown.at(-1)).toBe('Показаны первые 20. Уточните запрос.');
  });

  it('shows «Поиск…» while the search runs, and a failed search says so', async () => {
    await openForm('MSK-BUG-4', unassigned());
    let release: () => void = () => {};
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(
        CANDIDATES_URL,
        async () => {
          await held;
          return HttpResponse.json({ status: 500, detail: 'Сбой.' }, { status: 500 });
        },
        { once: true },
      ),
    );

    await search('анна');

    expect(await screen.findByRole('option', { name: 'Поиск…' })).toBeInTheDocument();
    release();
    expect(
      await screen.findByRole('option', {
        name: 'Не удалось выполнить поиск. Попробуйте ещё раз.',
      }),
    ).toHaveAttribute('aria-disabled', 'true');

    // Typing again searches again.
    await userEvent.type(assignee(), ' ');
    await userEvent.type(assignee(), '{Backspace}');
    await userEvent.type(assignee(), ' с');
    expect(await screen.findByRole('option', { name: /Анна Смирнова/ })).toBeInTheDocument();
  });
});

describe('mod-003 US-4 Scenario: An inactive user cannot be newly assigned', () => {
  it('does not offer «Пётр Иванов»', async () => {
    await openForm('MSK-BUG-4', unassigned());

    await search('Пётр');

    expect(await screen.findByRole('option', { name: 'Никого не найдено' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /Пётр Иванов/ })).not.toBeInTheDocument();
  });

  it('a refused save puts «Этого пользователя нельзя назначить исполнителем.» under «Исполнитель»', async () => {
    const api = await openForm('MSK-BUG-4', unassigned());
    await search('Кузнецова');
    await userEvent.click(await screen.findByRole('option', { name: /Анна Кузнецова/ }));
    // Her account is disabled before the save lands.
    const user = api.data.users.find((candidate) => candidate.id === ids.annaK);
    if (user) user.active = false;

    await userEvent.click(save());

    expect(
      await screen.findByText('Этого пользователя нельзя назначить исполнителем.'),
    ).toBeInTheDocument();
    expect(assignee()).toHaveAttribute('aria-invalid', 'true');
    expect(api.record(ids.msk, 4).assigneeId).toBeNull();
  });
});

describe('mod-003 US-4 Scenario: An assignee whose account is disabled later stays', () => {
  it('a title save keeps «Анна Смирнова», with no mark', async () => {
    const data = exampleDataset();
    const anna = data.users.find((candidate) => candidate.id === ids.anna);
    if (anna) anna.active = false;
    const api = await openForm('MSK-BUG-4', data);
    expect(assignee()).toHaveValue('Анна Смирнова');

    await userEvent.type(screen.getByRole('textbox', { name: 'Заголовок' }), '!');
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({ title: 'Падение при входе!' });
    expect(assignee()).toHaveValue('Анна Смирнова');
  });
});

describe('mod-003 US-4 Scenario: An assignment is cleared', () => {
  it('«Не назначен» sends `{ userId: null }` and the form reads «Не назначен»', async () => {
    const api = await openForm('MSK-BUG-4');

    await userEvent.click(assignee());
    await userEvent.click(await screen.findByRole('option', { name: 'Не назначен' }));
    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(patches(api)[0]?.body).toEqual({ assignee: { userId: null } });
    expect(assignee()).toHaveValue('');
    expect(screen.getByText('Не назначен')).toBeInTheDocument();
    expect(api.record(ids.msk, 4).assigneeId).toBeNull();
  });
});
