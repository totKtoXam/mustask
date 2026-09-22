import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { server } from '../../../test/mocks/server';
import { renderWithProviders } from '../../../test/renderWithProviders';
import { sampleLanguages } from '../testFixtures';
import { DefaultLanguageDialog } from './DefaultLanguageDialog';

const api = 'http://localhost:5000';
const defaultUrl = `${api}/api/dictionaries/languages/default`;

const target = { code: 'kk', endonym: 'Қазақша' };

describe('DefaultLanguageDialog', () => {
  it('показывает вопрос с эндонимом и кодом переданной строки', () => {
    renderWithProviders(<DefaultLanguageDialog language={target} onClose={vi.fn()} onAssigned={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Назначить языком по умолчанию' })).toBeInTheDocument();
    expect(screen.getByText('Сделать «Қазақша» (kk) языком по умолчанию?')).toBeInTheDocument();
    expect(
      screen.getByText(
        'Все активные записи должны быть переведены на этот язык — иначе изменение будет отклонено.',
      ),
    ).toBeInTheDocument();
  });

  it('закрыт, когда language === null', () => {
    renderWithProviders(<DefaultLanguageDialog language={null} onClose={vi.fn()} onAssigned={vi.fn()} />);

    expect(screen.queryByRole('heading', { name: 'Назначить языком по умолчанию' })).not.toBeInTheDocument();
  });

  it('"Отмена" вызывает onClose и не отправляет запрос', async () => {
    const putSpy = vi.fn();
    server.use(
      http.put(defaultUrl, () => {
        putSpy();
        return HttpResponse.json(sampleLanguages[1]);
      }),
    );
    const onClose = vi.fn();
    renderWithProviders(<DefaultLanguageDialog language={target} onClose={onClose} onAssigned={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Отмена' }));

    expect(onClose).toHaveBeenCalledTimes(1);
    expect(putSpy).not.toHaveBeenCalled();
  });

  it('"Назначить" вызывает PUT .../languages/default и передаёт результат в onAssigned', async () => {
    const assigned = { ...sampleLanguages[1], isDefault: true };
    server.use(http.put(defaultUrl, () => HttpResponse.json(assigned)));
    const onAssigned = vi.fn();
    renderWithProviders(<DefaultLanguageDialog language={target} onClose={vi.fn()} onAssigned={onAssigned} />);

    await userEvent.click(screen.getByRole('button', { name: 'Назначить' }));

    await waitFor(() => expect(onAssigned).toHaveBeenCalledWith(assigned));
  });

  it('во время сохранения кнопки недоступны и показан индикатор', async () => {
    server.use(
      http.put(defaultUrl, async () => {
        await new Promise((resolve) => setTimeout(resolve, 20));
        return HttpResponse.json(sampleLanguages[1]);
      }),
    );
    renderWithProviders(<DefaultLanguageDialog language={target} onClose={vi.fn()} onAssigned={vi.fn()} />);

    const assignButton = screen.getByRole('button', { name: 'Назначить' });
    await userEvent.click(assignButton);

    expect(screen.getByRole('button', { name: 'Отмена' })).toBeDisabled();
    expect(assignButton).toBeDisabled();
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  it('отказ 409 (язык неактивен) показывает текст сервера и не закрывает диалог', async () => {
    server.use(
      http.put(defaultUrl, () =>
        HttpResponse.json(
          {
            errorCode: 'dictionaries.languages.default_must_be_active',
            detail:
              'Языком по умолчанию может стать только активный язык (код «kk»).',
          },
          { status: 409 },
        ),
      ),
    );
    const onAssigned = vi.fn();
    const onClose = vi.fn();
    renderWithProviders(<DefaultLanguageDialog language={target} onClose={onClose} onAssigned={onAssigned} />);

    await userEvent.click(screen.getByRole('button', { name: 'Назначить' }));

    expect(
      await screen.findByText(
        'Языком по умолчанию может стать только активный язык (код «kk»).',
      ),
    ).toBeInTheDocument();
    expect(onAssigned).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('отказ 409 (язык более не активен) показывает текст сервера дословно', async () => {
    server.use(
      http.put(defaultUrl, () =>
        HttpResponse.json(
          {
            errorCode: 'dictionaries.languages.default_must_be_active',
            detail: 'Только активный язык может стать языком по умолчанию.',
          },
          { status: 409 },
        ),
      ),
    );
    renderWithProviders(<DefaultLanguageDialog language={target} onClose={vi.fn()} onAssigned={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Назначить' }));

    expect(await screen.findByText('Только активный язык может стать языком по умолчанию.')).toBeInTheDocument();
  });
});
