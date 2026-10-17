import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { UserCandidate } from '../api/membershipApi';
import type { UserSearchScope } from '../hooks/useUserSearch';
import { serveOrganizations } from '../testApi';
import { ids } from '../testFixtures';
import { membersDataset, people } from '../testMembersData';
import { serveMemberships } from '../testMembership';
import { UserSearchField } from './UserSearchField';

// mod-016 design § 3.5, § 4.4: the user search both add dialogs use. The
// dialogs' tests cover the lines and the refusals through the whole dialog;
// here the field alone: which endpoint a scope asks, and that it is a
// controlled field that shows the chosen user.

function Harness({
  scope,
  error,
  helperText,
}: {
  scope: UserSearchScope;
  error?: string;
  helperText?: string;
}) {
  const [value, setValue] = useState<UserCandidate | null>(null);
  return (
    <>
      <UserSearchField
        label="Пользователь"
        scope={scope}
        value={value}
        onChange={setValue}
        error={error}
        helperText={helperText}
      />
      <p data-testid="chosen">{value?.id ?? ''}</p>
    </>
  );
}

function renderField(props: Parameters<typeof Harness>[0]) {
  const data = membersDataset();
  serveOrganizations(data);
  const api = serveMemberships(data);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <Harness {...props} />
    </QueryClientProvider>,
  );
  return api;
}

async function type(text: string) {
  const field = screen.getByRole('combobox', { name: 'Пользователь' });
  await userEvent.click(field);
  await userEvent.type(field, text);
  return field;
}

describe('mod-016 design § 3.5, § 4.4: UserSearchField', () => {
  it('searches the organization’s directory in the organization scope', async () => {
    const api = renderField({ scope: { kind: 'organization', organizationId: ids.alfa } });

    await type('Ма');
    await screen.findByRole('option', { name: /Мария Орлова/ });

    expect(api.requests.map((request) => request.path)).toEqual([
      `/api/organizations/${ids.alfa}/member-candidates?text=${encodeURIComponent('Ма')}`,
    ]);
  });

  it('searches the active members of the organization in the team scope: a user in no organization is not offered', async () => {
    const api = renderField({
      scope: {
        kind: 'team',
        organizationId: ids.alfa,
        teamId: ids.platformTeam,
      },
    });

    await type('Ива');

    // Иван Петров is a member and active; Мария Орлова, who is in no organization, is not matched by «Ива» anyway.
    expect(await screen.findByRole('option', { name: /Иван Петров/ })).toBeVisible();
    expect(api.requests.map((request) => request.path)).toEqual([
      `/api/organizations/${ids.alfa}/teams/${ids.platformTeam}/member-candidates?text=${encodeURIComponent('Ива')}`,
    ]);

    const field = screen.getByRole('combobox', { name: 'Пользователь' });
    await userEvent.clear(field);
    await userEvent.type(field, 'Мар');
    expect(await screen.findByRole('option', { name: 'Пользователи не найдены' })).toBeVisible();
  });

  it('shows the chosen user’s name in the field and reports them to its owner', async () => {
    renderField({ scope: { kind: 'organization', organizationId: ids.alfa } });

    const field = await type('Мария');
    await userEvent.click(await screen.findByRole('option', { name: /Мария Орлова/ }));

    expect(field).toHaveValue('Мария Орлова');
    expect(screen.getByTestId('chosen')).toHaveTextContent(people.maria.id);
  });

  it('marks the field required without a native constraint, and shows a refusal in place of the helper', async () => {
    renderField({
      scope: { kind: 'organization', organizationId: ids.alfa },
      helperText: 'Подсказка',
      error: 'Пользователь уже состоит в организации',
    });

    const field = screen.getByRole('combobox', { name: 'Пользователь' });
    expect(field).toHaveAttribute('aria-required', 'true');
    expect(field).not.toHaveAttribute('required');
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Пользователь уже состоит в организации')).toBeVisible();
    expect(screen.queryByText('Подсказка')).not.toBeInTheDocument();
  });

  it('shows the helper while there is no refusal', async () => {
    renderField({
      scope: { kind: 'organization', organizationId: ids.alfa },
      helperText: 'Подсказка',
    });

    await waitFor(() => expect(screen.getByText('Подсказка')).toBeVisible());
    expect(screen.getByRole('combobox', { name: 'Пользователь' })).toHaveAttribute(
      'aria-invalid',
      'false',
    );
  });
});
