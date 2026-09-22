import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import dayjs from 'dayjs';
import { VersionConflictPanel } from './VersionConflictPanel';
import type { ConflictField } from '../hooks/useConflictRecovery';

const threeFields: ConflictField[] = [
  { label: 'Эндоним', mine: 'Қазақша', server: 'Қазақ тілі' },
  { label: 'Порядок', mine: '20', server: '20' },
  { label: 'Активен', mine: 'нет', server: 'да' },
];

// Сырой ISO, как его отдаёт сервер (`updatedAt`) — панель форматирует сама,
// поэтому тест кормит её тем же сырым видом, что и места вызова, а не
// готовой строкой.
const serverUpdatedAtIso = '2026-09-12T14:03:00Z';
const serverUpdatedAtFormatted = dayjs(serverUpdatedAtIso).format('DD.MM.YYYY HH:mm');

function renderPanel(status: 'checking' | 'comparing', overrides: { onKeepMine?: () => void; onAcceptServer?: () => void } = {}) {
  const onKeepMine = overrides.onKeepMine ?? vi.fn();
  const onAcceptServer = overrides.onAcceptServer ?? vi.fn();
  render(
    <VersionConflictPanel
      status={status}
      fields={threeFields}
      mine={{ updatedAt: null, updatedBy: null }}
      server={{ updatedAt: serverUpdatedAtIso, updatedBy: 'admin2' }}
      onKeepMine={onKeepMine}
      onAcceptServer={onAcceptServer}
    />,
  );
  return { onKeepMine, onAcceptServer };
}

describe('VersionConflictPanel', () => {
  it('checking: показывает только строку со спиннером, без сравнения и кнопок', () => {
    renderPanel('checking');

    expect(screen.getByText('Проверяем текущую версию…')).toBeInTheDocument();
    expect(screen.queryByText(/изменена другим пользователем/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Сохранить свой вариант' })).not.toBeInTheDocument();
  });

  it('comparing: показывает предупреждение, таблицу сравнения и обе кнопки', () => {
    renderPanel('comparing');

    expect(screen.getByText(/изменена другим пользователем/)).toBeInTheDocument();
    expect(screen.getByText('Эндоним')).toBeInTheDocument();
    expect(screen.getByText('Қазақша')).toBeInTheDocument();
    expect(screen.getByText('Қазақ тілі')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Принять серверный вариант' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Сохранить свой вариант' })).toBeInTheDocument();
  });

  it('чип "изменилось" — только на строке, где мои и серверные значения расходятся', () => {
    renderPanel('comparing');

    const rows = screen.getAllByRole('row').slice(1); // без строки заголовка
    const changedRows = rows.filter((row) => row.textContent?.includes('изменилось'));
    expect(changedRows).toHaveLength(2); // "Эндоним" и "Активен" различаются, "Порядок" — нет
  });

  it('"ваш черновик", когда mine.updatedAt === null', () => {
    renderPanel('comparing');
    expect(screen.getByText(/— \(ваш черновик\)/)).toBeInTheDocument();
  });

  // панель сама форматирует сырой ISO серверного штампа —
  // раньше это делало (или забывало сделать) каждое место вызова
  // отдельно, и один из них (LanguageFormPage.tsx) рендерил необработанную
  // строку целиком.
  it('серверный штамп форматируется панелью из сырого ISO', () => {
    renderPanel('comparing');
    expect(screen.getByText(`Обновлено: ${serverUpdatedAtFormatted} — admin2`)).toBeInTheDocument();
    expect(screen.queryByText(new RegExp(serverUpdatedAtIso.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))).not.toBeInTheDocument();
  });

  // `formatDateTime` (`shared/lib/format.ts`):
  // пустой штамп рендерится как `—`, а не как `Invalid Date`. Недостижимо
  // сегодня через `useConflictRecovery` (сервер выставляется до
  // `'comparing'`), но места вызова передают `server.updatedAt ?? ''`,
  // если сервер когда-нибудь не отдаст штамп.
  it('пустой server.updatedAt рендерится как «—», а не «Invalid Date»', () => {
    render(
      <VersionConflictPanel
        status="comparing"
        fields={threeFields}
        mine={{ updatedAt: null, updatedBy: null }}
        server={{ updatedAt: '', updatedBy: 'admin2' }}
        onKeepMine={vi.fn()}
        onAcceptServer={vi.fn()}
      />,
    );
    expect(screen.getByText('Обновлено: — — admin2')).toBeInTheDocument();
    expect(screen.queryByText(/Invalid Date/)).not.toBeInTheDocument();
  });

  it('"Сохранить свой вариант" вызывает onKeepMine и ничего больше', async () => {
    const user = userEvent.setup();
    const { onKeepMine, onAcceptServer } = renderPanel('comparing');

    await user.click(screen.getByRole('button', { name: 'Сохранить свой вариант' }));

    expect(onKeepMine).toHaveBeenCalledTimes(1);
    expect(onAcceptServer).not.toHaveBeenCalled();
  });

  it('"Принять серверный вариант" вызывает onAcceptServer и ничего больше', async () => {
    const user = userEvent.setup();
    const { onKeepMine, onAcceptServer } = renderPanel('comparing');

    await user.click(screen.getByRole('button', { name: 'Принять серверный вариант' }));

    expect(onAcceptServer).toHaveBeenCalledTimes(1);
    expect(onKeepMine).not.toHaveBeenCalled();
  });
});
