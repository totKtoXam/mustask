import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ContextSelectors, type ContextSelectorsProps } from './ContextSelectors';
import { NO_SELECTION, type FitData, type ObjectSelection } from '../navigation/selectorFit';

// feat-043 design § 3, US-8: the four header selectors over the US-8 cast. «Логистика» holds A
// and B and is linked to MARS and VENUS; «Финансы» holds C; MARS is linked to A, B and C, VENUS
// to A; D and JUPITER have no link; VENUS is the user's own.

const DATA: FitData = {
  departments: [
    { id: 'fin', name: 'Финансы' },
    { id: 'log', name: 'Логистика' },
  ],
  projects: [
    { id: 'a', name: 'A', key: 'A', departmentId: 'log', teamIds: ['mars', 'venus'] },
    { id: 'b', name: 'B', key: 'B', departmentId: 'log', teamIds: ['mars'] },
    { id: 'c', name: 'C', key: 'C', departmentId: 'fin', teamIds: ['mars'] },
    { id: 'd', name: 'D', key: 'D', departmentId: null, teamIds: [] },
  ],
  teams: [
    { id: 'jupiter', name: 'JUPITER', isMember: false, departmentIds: [] },
    { id: 'mars', name: 'MARS', isMember: false, departmentIds: ['log'] },
    { id: 'venus', name: 'VENUS', isMember: true, departmentIds: ['log'] },
  ],
};

const ORGANIZATIONS = [
  { id: 'o', name: 'Ромашка' },
  { id: 'p', name: 'Лютик' },
];

interface HostProps extends Partial<ContextSelectorsProps> {
  initial?: Partial<ObjectSelection>;
  onNotice?: (notice: string | null) => void;
}

function Host({ initial = {}, onNotice, ...rest }: HostProps) {
  const [selection, setSelection] = useState<ObjectSelection>({ ...NO_SELECTION, ...initial });
  return (
    <ContextSelectors
      organizations={ORGANIZATIONS}
      organizationId="o"
      data={DATA}
      dataStatus="ready"
      onRetry={vi.fn()}
      selection={selection}
      onOrganization={vi.fn()}
      onAllOrganizations={vi.fn()}
      onChange={(next, notice) => {
        setSelection(next);
        onNotice?.(notice);
      }}
      {...rest}
    />
  );
}

const combo = (name: string) => screen.getByRole('combobox', { name, hidden: true });

describe('feat-043 US-8: the four selectors', () => {
  it('shows the four captions with «все» and the organization name', () => {
    render(<Host />);
    expect(combo('Организация: Ромашка')).toBeInTheDocument();
    for (const caption of ['Департамент', 'Команда', 'Проект']) {
      expect(combo(`${caption}: все`)).toBeInTheDocument();
    }
  });

  it('marks a chosen value and shows its name', () => {
    render(<Host initial={{ team: 'mars' }} />);
    expect(combo('Команда: MARS')).toBeInTheDocument();
  });

  it('a fitting choice resets nothing and gives no notice', async () => {
    const user = userEvent.setup();
    const onNotice = vi.fn();
    render(<Host initial={{ department: 'log' }} onNotice={onNotice} />);
    await user.click(combo('Проект: все'));
    await user.click(await screen.findByRole('option', { name: /^A/ }));
    expect(onNotice).toHaveBeenLastCalledWith(null);
    expect(combo('Департамент: Логистика')).toBeInTheDocument();
    expect(combo('Проект: A')).toBeInTheDocument();
  });

  it('lists what does not fit under «Остальные», and choosing it resets with the notice', async () => {
    const user = userEvent.setup();
    const onNotice = vi.fn();
    render(<Host initial={{ department: 'log' }} onNotice={onNotice} />);
    await user.click(combo('Проект: все'));
    const listbox = await screen.findByRole('listbox', { name: 'Проект' });
    expect(within(listbox).getByText('Остальные')).toBeInTheDocument();
    expect(within(listbox).getByText('Выбор сбросит то, что не подойдёт.')).toBeInTheDocument();
    await user.click(within(listbox).getByRole('option', { name: /^C/ }));
    expect(onNotice).toHaveBeenLastCalledWith('Выбор сброшен: департамент Логистика.');
    expect(combo('Департамент: все')).toBeInTheDocument();
    expect(combo('Проект: C')).toBeInTheDocument();
  });

  it('splits the teams into «Мои команды» and «Другие команды»', async () => {
    const user = userEvent.setup();
    render(<Host />);
    await user.click(combo('Команда: все'));
    const listbox = await screen.findByRole('listbox', { name: 'Команда' });
    expect(within(listbox).getByText('Мои команды')).toBeInTheDocument();
    expect(within(listbox).getByText('Другие команды')).toBeInTheDocument();
    const names = within(listbox)
      .getAllByRole('option')
      .map((option) => option.textContent);
    expect(names.indexOf('VENUS')).toBeLessThan(names.indexOf('JUPITER'));
  });

  it('the «все» row widens the selection without a notice', async () => {
    const user = userEvent.setup();
    const onNotice = vi.fn();
    render(<Host initial={{ team: 'mars' }} onNotice={onNotice} />);
    await user.click(combo('Команда: MARS'));
    await user.click(await screen.findByRole('option', { name: 'все' }));
    expect(onNotice).toHaveBeenLastCalledWith(null);
    expect(combo('Команда: все')).toBeInTheDocument();
  });

  it('«Все организации» clears all four; another organization is chosen by name', async () => {
    const user = userEvent.setup();
    const onAllOrganizations = vi.fn();
    const onOrganization = vi.fn();
    render(<Host onAllOrganizations={onAllOrganizations} onOrganization={onOrganization} />);
    await user.click(combo('Организация: Ромашка'));
    await user.click(await screen.findByRole('option', { name: 'Лютик' }));
    expect(onOrganization).toHaveBeenCalledWith('p');
    await user.click(combo('Организация: Ромашка'));
    await user.click(await screen.findByRole('option', { name: 'Все организации' }));
    expect(onAllOrganizations).toHaveBeenCalledTimes(1);
  });

  it('shows the loading and error states of the object selectors, with the retry', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(<Host data={undefined} dataStatus="error" onRetry={onRetry} />);
    await user.click(combo('Проект: все'));
    expect(
      await screen.findByText('Не удалось загрузить список. Попробуйте ещё раз.'),
    ).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
