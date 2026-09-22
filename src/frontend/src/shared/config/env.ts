// Конфигурация окружения. Единственное место, читающее import.meta.env —
// остальной код получает значения отсюда.

const configuredApiBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;

if (!configuredApiBaseUrl) {
  // Каркас должен падать явно, если базовый URL API не задан, а не молча
  // слать запросы в никуда.
  throw new Error('VITE_API_BASE_URL не задан. Укажите его в .env (см. .env.example).');
}

/**
 * Адрес API для текущей вкладки.
 *
 * `VITE_API_BASE_URL` вшивается в бандл на этапе сборки, и в стенде Aspire
 * это всегда `http://localhost:<порт>`. Пока панель открывают на той же
 * машине, всё сходится; но стенд доступен и по сети, а там вшитый localhost
 * означает localhost КЛИЕНТА, где никакого API нет. Симптом — «не удалось
 * соединиться с API» на форме входа (проверено живым запуском: страница
 * грузится, вход не проходит). Chrome вдобавок блокирует запросы со
 * страницы в приватной сети на loopback (Private Network Access), так что
 * не спасает даже открытие по IP на самой машине с API.
 *
 * Поэтому loopback в настройке трактуется как «тот же хост, что и
 * страница»: API слушает все интерфейсы и доступен по тому же адресу, по
 * которому открыт фронтенд. Явно заданный внешний адрес (не loopback) не
 * трогаем — его выбрали осознанно.
 */
function resolveApiBaseUrl(configured: string): string {
  const isLoopback = (host: string) => host === 'localhost' || host === '127.0.0.1';

  try {
    const url = new URL(configured, window.location.origin);
    if (isLoopback(url.hostname) && !isLoopback(window.location.hostname)) {
      url.protocol = window.location.protocol;
      url.hostname = window.location.hostname;
    }
    return url.toString().replace(/\/$/, '');
  } catch {
    // Настройка не разбирается как URL — отдаём как есть: пусть ошибка
    // всплывёт на первом же запросе, а не превратится в тихую подмену.
    return configured;
  }
}

export const env = {
  apiBaseUrl: resolveApiBaseUrl(configuredApiBaseUrl),
} as const;
