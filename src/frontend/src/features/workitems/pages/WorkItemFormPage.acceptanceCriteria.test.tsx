import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, within } from '@testing-library/react';

vi.setConfig({ testTimeout: 120_000 });
import userEvent from '@testing-library/user-event';
import { ITEM_OF_TYPE, openFieldsForm, patches, save, section, textbox } from '../testFields';
import { hierarchyDataset, recordOf } from '../testFixtures';

describe('Acceptance Criteria: Dynamic Scenario List (R2, R3, R4, AC.1)', () => {
  it('renders existing acceptance criteria with GIVEN, WHEN, THEN fields and scenario header', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).fields.acceptanceCriteria = [
      {
        given: 'пользователь на странице логина',
        when: 'вводит корректный логин и пароль',
        then: 'происходит переход в личный кабинет',
      },
    ];

    await openFieldsForm(ITEM_OF_TYPE['Пользовательская история'], data);

    const acSection = section('Критерии приёмки');
    expect(acSection).toBeInTheDocument();
    expect(within(acSection).getByText('Сценарий 1')).toBeInTheDocument();
    expect(textbox('GIVEN (предусловие)')).toHaveValue('пользователь на странице логина');
    expect(textbox('WHEN (действие)')).toHaveValue('вводит корректный логин и пароль');
    expect(textbox('THEN (ожидаемый результат)')).toHaveValue(
      'происходит переход в личный кабинет',
    );
    expect(screen.getByRole('button', { name: 'Удалить критерий' })).toBeInTheDocument();
    expect(save()).toBeDisabled();
  });

  it('adds a new criterion scenario on clicking «Добавить критерий» and enables save button', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).fields.acceptanceCriteria = [];

    await openFieldsForm(ITEM_OF_TYPE['Пользовательская история'], data);

    expect(screen.getByText('Критерии приёмки не заданы.')).toBeInTheDocument();
    expect(save()).toBeDisabled();

    const addButton = screen.getByRole('button', { name: 'Добавить критерий' });
    await userEvent.click(addButton);

    expect(screen.queryByText('Критерии приёмки не заданы.')).not.toBeInTheDocument();
    expect(screen.getByText('Сценарий 1')).toBeInTheDocument();
    expect(textbox('GIVEN (предусловие)')).toHaveValue('');
    expect(textbox('WHEN (действие)')).toHaveValue('');
    expect(textbox('THEN (ожидаемый результат)')).toHaveValue('');
    expect(save()).toBeEnabled();

    // Adding second criterion
    await userEvent.click(addButton);
    expect(screen.getByText('Сценарий 2')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Удалить критерий' })).toHaveLength(2);
  });

  it('deletes a criterion scenario on clicking delete button and updates list', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).fields.acceptanceCriteria = [
      {
        given: 'предусловие 1',
        when: 'действие 1',
        then: 'результат 1',
      },
      {
        given: 'предусловие 2',
        when: 'действие 2',
        then: 'результат 2',
      },
    ];

    await openFieldsForm(ITEM_OF_TYPE['Пользовательская история'], data);

    expect(screen.getByText('Сценарий 1')).toBeInTheDocument();
    expect(screen.getByText('Сценарий 2')).toBeInTheDocument();
    expect(save()).toBeDisabled();

    const deleteButtons = screen.getAllByRole('button', { name: 'Удалить критерий' });
    fireEvent.click(deleteButtons[0]!);

    expect(screen.queryByText('Сценарий 2')).not.toBeInTheDocument();
    expect(screen.getByText('Сценарий 1')).toBeInTheDocument();
    expect(textbox('GIVEN (предусловие)')).toHaveValue('предусловие 2');
    expect(save()).toBeEnabled();
  });

  it('editing GIVEN, WHEN, THEN toggles dirty state and enables «Сохранить»', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).fields.acceptanceCriteria = [
      {
        given: 'исходное дано',
        when: 'исходное когда',
        then: 'исходное тогда',
      },
    ];

    await openFieldsForm(ITEM_OF_TYPE['Пользовательская история'], data);
    expect(save()).toBeDisabled();

    fireEvent.change(textbox('GIVEN (предусловие)'), {
      target: { value: 'исходное дано + правка' },
    });
    expect(save()).toBeEnabled();

    fireEvent.change(textbox('GIVEN (предусловие)'), { target: { value: 'исходное дано' } });
    expect(save()).toBeDisabled();

    fireEvent.change(textbox('WHEN (действие)'), {
      target: { value: 'исходное когда + действие' },
    });
    expect(save()).toBeEnabled();

    fireEvent.change(textbox('WHEN (действие)'), { target: { value: 'исходное когда' } });
    expect(save()).toBeDisabled();

    fireEvent.change(textbox('THEN (ожидаемый результат)'), {
      target: { value: 'исходное тогда + результат' },
    });
    expect(save()).toBeEnabled();
  });

  it('saves updated criteria via PATCH and resets dirty state', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).fields.acceptanceCriteria = [];

    const { api } = await openFieldsForm(ITEM_OF_TYPE['Пользовательская история'], data);

    await userEvent.click(screen.getByRole('button', { name: 'Добавить критерий' }));
    fireEvent.change(textbox('GIVEN (предусловие)'), {
      target: { value: 'пользователь в корзине' },
    });
    fireEvent.change(textbox('WHEN (действие)'), { target: { value: 'нажимает оформить' } });
    fireEvent.change(textbox('THEN (ожидаемый результат)'), {
      target: { value: 'создается заказ' },
    });

    await userEvent.click(save());

    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();
    expect(save()).toBeDisabled();

    expect(patches(api).map((req) => req.body)).toEqual([
      {
        acceptanceCriteria: [
          {
            given: 'пользователь в корзине',
            when: 'нажимает оформить',
            then: 'создается заказ',
          },
        ],
      },
    ]);
  });

  it.each([
    ['Эпик', ITEM_OF_TYPE['Эпик']],
    ['Фича', ITEM_OF_TYPE['Фича']],
    ['Задача', ITEM_OF_TYPE['Задача']],
    ['Баг', ITEM_OF_TYPE['Баг']],
  ])(
    'does not render acceptance criteria section for %s (R4 backward compatibility)',
    async (_type, identifier) => {
      await openFieldsForm(identifier);

      expect(screen.queryByRole('region', { name: 'Критерии приёмки' })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Добавить критерий' })).not.toBeInTheDocument();
      expect(screen.queryByLabelText('GIVEN (предусловие)')).not.toBeInTheDocument();
    },
  );

  it('deletes scenario from the middle of three criteria and correctly re-indexes remaining scenarios', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).fields.acceptanceCriteria = [
      { given: 'G1', when: 'W1', then: 'T1' },
      { given: 'G2_to_delete', when: 'W2_to_delete', then: 'T2_to_delete' },
      { given: 'G3', when: 'W3', then: 'T3' },
    ];

    await openFieldsForm(ITEM_OF_TYPE['Пользовательская история'], data);

    expect(screen.getByText('Сценарий 1')).toBeInTheDocument();
    expect(screen.getByText('Сценарий 2')).toBeInTheDocument();
    expect(screen.getByText('Сценарий 3')).toBeInTheDocument();

    const deleteButtons = screen.getAllByRole('button', { name: 'Удалить критерий' });
    expect(deleteButtons).toHaveLength(3);

    // Delete middle scenario (index 1)
    fireEvent.click(deleteButtons[1]!);

    expect(screen.queryByText('Сценарий 3')).not.toBeInTheDocument();
    expect(screen.getByText('Сценарий 1')).toBeInTheDocument();
    expect(screen.getByText('Сценарий 2')).toBeInTheDocument();

    const givenInputs = screen.getAllByLabelText('GIVEN (предусловие)');
    expect(givenInputs).toHaveLength(2);
    expect(givenInputs[0]).toHaveValue('G1');
    expect(givenInputs[1]).toHaveValue('G3');
    expect(save()).toBeEnabled();
  });

  it('allows saving newly added empty criterion scenario via PATCH', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).fields.acceptanceCriteria = [];

    const { api } = await openFieldsForm(ITEM_OF_TYPE['Пользовательская история'], data);

    await userEvent.click(screen.getByRole('button', { name: 'Добавить критерий' }));
    expect(save()).toBeEnabled();

    await userEvent.click(save());
    expect(await screen.findByText('Изменения сохранены.')).toBeInTheDocument();

    expect(patches(api).map((req) => req.body)).toEqual([
      {
        acceptanceCriteria: [
          {
            given: '',
            when: '',
            then: '',
          },
        ],
      },
    ]);
  });

  it('handles multiline and long text in GIVEN, WHEN, THEN scenarios without errors', async () => {
    const data = hierarchyDataset();
    const multilineGiven = 'Line 1: Предусловие\nLine 2: Контекст системы\nLine 3: Детали окружения';
    const longWhen = 'WHEN '.repeat(200);
    const longThen = 'THEN '.repeat(200);

    recordOf(data, 2).fields.acceptanceCriteria = [
      { given: multilineGiven, when: longWhen, then: longThen },
    ];

    await openFieldsForm(ITEM_OF_TYPE['Пользовательская история'], data);

    expect(textbox('GIVEN (предусловие)')).toHaveValue(multilineGiven);
    expect(textbox('WHEN (действие)')).toHaveValue(longWhen);
    expect(textbox('THEN (ожидаемый результат)')).toHaveValue(longThen);
    expect(save()).toBeDisabled();
  });

  it('renders and supports high scenario counts (15 scenarios) without errors', async () => {
    const data = hierarchyDataset();
    recordOf(data, 2).fields.acceptanceCriteria = Array.from({ length: 15 }, (_, i) => ({
      given: `Precondition #${i + 1}`,
      when: `Action #${i + 1}`,
      then: `Result #${i + 1}`,
    }));

    await openFieldsForm(ITEM_OF_TYPE['Пользовательская история'], data);

    expect(screen.getByText('Сценарий 1')).toBeInTheDocument();
    expect(screen.getByText('Сценарий 15')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Удалить критерий' })).toHaveLength(15);
    expect(save()).toBeDisabled();
  });
});
