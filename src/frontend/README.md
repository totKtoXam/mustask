# frontend

Веб-панель Mustask — трекера задач — поверх `Mustask.Api`. React +
TypeScript + Vite + MUI + React Query.

## Разделы

- **Главная** — стартовая страница после входа.
- **Языки** — справочник языков: реестр с фильтрами, сортировкой и
  пагинацией, форма создания и редактирования языка, назначение языка по
  умолчанию. Пункт меню и действия скрываются по правам пользователя
  (`dictionaries.languages.read`, `dictionaries.languages.write`).

Каждый список открывает детали отдельной страницей, а не диалогом, — на них
можно сослаться и вернуться по истории браузера.

## Запуск

Обычный путь — целиком через Aspire (поднимет API, Keycloak и панель):

```bash
dotnet run --project src/backend/Mustask.AppHost
```

Отдельно, против уже запущенного API:

```bash
cd src/frontend
npm ci
cp .env.example .env
npm run dev
```

Открывается на http://localhost:3000 (порт пришпилен: он в CORS-списке
`Mustask.Api`), слушает все интерфейсы — с другой машины доступен по
`http://<ip>:3000`.

Вход — через Keycloak (realm `mustask`, клиент `mustask-panel`); пользователи
и их группы заводятся в консоли Keycloak (ADR-0018). Панель сама пароль не
принимает, сессию ведёт `Mustask.Api` (ADR-0022).

## Команды

- `npm run dev` — dev-сервер Vite;
- `npm run build` — проверка типов (`tsc -b`) + production-сборка
  (требует `VITE_API_BASE_URL`);
- `npm test` — Vitest (Testing Library + MSW) однократно;
- `npm run lint` / `npm run format` / `npm run format:check`;
- `npm run e2e` — Playwright против живого стенда (см. `AGENTS.md`).
