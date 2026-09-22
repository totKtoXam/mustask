import { describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { ApiError } from '../../../shared/api/httpClient';
import { useConflictRecovery } from './useConflictRecovery';

interface FakeInput {
  name: string;
}
interface FakeServer {
  name: string;
  version: string;
  updatedAt: string;
  updatedBy: string;
}

const versionConflict = () => new ApiError(409, { errorCode: 'dictionaries.version_conflict' });
const versionRequired = () =>
  new ApiError(400, {
    errorCode: 'dictionaries.validation_failed',
    errors: { ifMatch: [{ code: 'dictionaries.version_required', detail: 'нет версии' }] },
  });

function setup(save: (input: FakeInput, ifMatch: string) => Promise<FakeServer>, reread: () => Promise<FakeServer>) {
  return renderHook(() =>
    useConflictRecovery<FakeInput, FakeServer, FakeServer>({
      save,
      reread,
      toFields: (mine, server) => [{ label: 'Название', mine: mine.name, server: server.name }],
      getVersion: (server) => server.version,
    }),
  );
}

describe('useConflictRecovery', () => {
  it('успешное сохранение возвращает в idle, ничего не остаётся в fields/server', async () => {
    const save = vi.fn().mockResolvedValue({ name: 'x', version: '2', updatedAt: 'a', updatedBy: 'b' });
    const reread = vi.fn();
    const { result } = setup(save, reread);

    let returned: FakeServer | undefined;
    await act(async () => {
      returned = await result.current.submit({ name: 'x' }, '1');
    });

    expect(returned?.version).toBe('2');
    expect(result.current.status).toBe('idle');
    expect(result.current.fields).toEqual([]);
    expect(result.current.server).toBeNull();
    expect(reread).not.toHaveBeenCalled();
  });

  it('409 version_conflict ведёт в checking -> comparing с полями "моё/серверное"', async () => {
    const save = vi.fn().mockRejectedValue(versionConflict());
    const reread = vi.fn().mockResolvedValue({ name: 'server-name', version: '5', updatedAt: 'a', updatedBy: 'admin2' });
    const { result } = setup(save, reread);

    await act(async () => {
      await result.current.submit({ name: 'my-name' }, '1');
    });

    expect(result.current.status).toBe('comparing');
    expect(result.current.fields).toEqual([{ label: 'Название', mine: 'my-name', server: 'server-name' }]);
    expect(result.current.server).toEqual({ name: 'server-name', version: '5', updatedAt: 'a', updatedBy: 'admin2' });
  });

  it('400 validation_failed с errors.ifMatch/version_required ведёт в тот же comparing (сходящийся случай)', async () => {
    const save = vi.fn().mockRejectedValue(versionRequired());
    const reread = vi.fn().mockResolvedValue({ name: 'server-name', version: '5', updatedAt: 'a', updatedBy: 'admin2' });
    const { result } = setup(save, reread);

    await act(async () => {
      await result.current.submit({ name: 'my-name' }, '1');
    });

    expect(result.current.status).toBe('comparing');
  });

  it('ошибка, не являющаяся конфликтом версии, пробрасывается наружу и не трогает status', async () => {
    const otherError = new ApiError(400, { errorCode: 'dictionaries.value_too_long' });
    const save = vi.fn().mockRejectedValue(otherError);
    const reread = vi.fn();
    const { result } = setup(save, reread);

    await expect(
      act(async () => {
        await result.current.submit({ name: 'x' }, '1');
      }),
    ).rejects.toThrow();

    expect(result.current.status).toBe('idle');
    expect(reread).not.toHaveBeenCalled();
  });

  it('keepMine повторно шлёт save с версией из перечитывания, а не с исходной', async () => {
    const save = vi
      .fn()
      .mockRejectedValueOnce(versionConflict())
      .mockResolvedValueOnce({ name: 'my-name', version: '6', updatedAt: 'a', updatedBy: 'me' });
    const reread = vi.fn().mockResolvedValue({ name: 'server-name', version: '5', updatedAt: 'a', updatedBy: 'admin2' });
    const { result } = setup(save, reread);

    await act(async () => {
      await result.current.submit({ name: 'my-name' }, '1');
    });
    expect(result.current.status).toBe('comparing');

    await act(async () => {
      await result.current.keepMine({ name: 'my-name' });
    });

    expect(save).toHaveBeenLastCalledWith({ name: 'my-name' }, '5');
    expect(result.current.status).toBe('idle');
    expect(result.current.server).toBeNull();
  });

  it('второй конфликт (ещё один гонщик) повторяет цикл с шага 1, а не сдаётся', async () => {
    const save = vi.fn().mockRejectedValue(versionConflict()); // конфликтует каждый раз
    const reread = vi
      .fn()
      .mockResolvedValueOnce({ name: 'server-v5', version: '5', updatedAt: 'a', updatedBy: 'admin2' })
      .mockResolvedValueOnce({ name: 'server-v9', version: '9', updatedAt: 'a', updatedBy: 'admin3' });
    const { result } = setup(save, reread);

    await act(async () => {
      await result.current.submit({ name: 'my-name' }, '1');
    });
    expect(result.current.status).toBe('comparing');
    expect(result.current.server?.version).toBe('5');

    await act(async () => {
      await result.current.keepMine({ name: 'my-name' });
    });

    // Второй раз ушли снова в 'comparing' с ещё более свежей версией сервера —
    // цикл повторился, а не завершился отказом.
    expect(result.current.status).toBe('comparing');
    expect(result.current.server?.version).toBe('9');
    expect(save).toHaveBeenNthCalledWith(2, { name: 'my-name' }, '5');
  });

  it('acceptServer отдаёт перечитанные данные и возвращает в idle, не отправляя запрос', async () => {
    const save = vi.fn().mockRejectedValue(versionConflict());
    const reread = vi.fn().mockResolvedValue({ name: 'server-name', version: '5', updatedAt: 'a', updatedBy: 'admin2' });
    const { result } = setup(save, reread);

    await act(async () => {
      await result.current.submit({ name: 'my-name' }, '1');
    });

    let accepted: FakeServer | null = null;
    act(() => {
      accepted = result.current.acceptServer();
    });

    expect(accepted).toEqual({ name: 'server-name', version: '5', updatedAt: 'a', updatedBy: 'admin2' });
    expect(result.current.status).toBe('idle');
    expect(result.current.server).toBeNull();
    expect(save).toHaveBeenCalledTimes(1); // только исходная попытка — accept ничего не шлёт
  });

  it('уход с панели (reset) отбрасывает черновик — ничего не было отправлено', async () => {
    const save = vi.fn().mockRejectedValue(versionConflict());
    const reread = vi.fn().mockResolvedValue({ name: 'server-name', version: '5', updatedAt: 'a', updatedBy: 'admin2' });
    const { result } = setup(save, reread);

    await act(async () => {
      await result.current.submit({ name: 'my-name' }, '1');
    });
    expect(result.current.status).toBe('comparing');

    act(() => {
      result.current.reset();
    });

    expect(result.current.status).toBe('idle');
    expect(result.current.server).toBeNull();
    expect(result.current.fields).toEqual([]);
    expect(save).toHaveBeenCalledTimes(1); // reset не шлёт ничего нового
  });

  it('перечитывание после конфликта тоже падает — возвращаемся в idle, а не зависаем в checking', async () => {
    const save = vi.fn().mockRejectedValue(versionConflict());
    const reread = vi.fn().mockRejectedValue(new ApiError(500, null));
    const { result } = setup(save, reread);

    let thrown: unknown;
    await act(async () => {
      try {
        await result.current.submit({ name: 'my-name' }, '1');
      } catch (error) {
        thrown = error;
      }
    });

    expect(thrown).toBeInstanceOf(ApiError);
    expect(result.current.status).toBe('idle');
  });
});
