# Runbook: MUSTASK на одной обычной VM

> Русский перевод [vm.md](vm.md) для DevOps. Оригинал — английский `vm.md`:
> если они расходятся, верен оригинал. Каждая правка `vm.md` вносится и сюда
> в том же изменении.

Развёртывание MUSTASK на одной Linux-VM без Docker и Kubernetes, с входом через
**существующий общий realm Keycloak** компании. Наше в этом realm — только два
клиента и четыре роли клиента; всё остальное в realm принадлежит компании
(ADR-0033 в репозитории: `docs/adr/0033-shared-realm-client-roles.md`).

Схема: один домен, один reverse proxy с TLS. Прокси отдаёт собранную панель
(статические файлы) и пересылает `/api/` в `Mustask.Api` на loopback-порт.
PostgreSQL работает на этой же VM.

```
браузер ──https──> nginx (mustask.example.com; под путём: portal.example.com/mustask, см. ниже)
                     ├─ /        → /opt/mustask/current/panel (сборка панели)
                     └─ /api/    → http://127.0.0.1:5000 (Mustask.Api, systemd)
Mustask.Api ──> PostgreSQL            Mustask.Api ──https──> общий Keycloak
```

Ниже `mustask.example.com` означает ваш домен, `keycloak.example.com` — хост
общего Keycloak.

**Разделы 2–9 автоматизированы** китом в `deploy/vm/` репозитория: скрипт
сборки, unit systemd, сайт nginx, шаблон env, скрипт установки/обновления и
smoke-проверка. Весь путь, когда сторона Keycloak (раздел 1) готова:

```bash
# машина сборки (раздел 4)
deploy/vm/build-release.sh v0.1.0 https://mustask.example.com
scp out/mustask-v0.1.0.tar.gz <vm>:

# VM, один раз (разделы 2, 3, 5, 7)
tar -xzf mustask-v0.1.0.tar.gz
sudo bash mustask-v0.1.0/kit/install.sh prepare --host mustask.example.com \
    --keycloak-host keycloak.example.com --realm <realm> --install-packages --create-db
sudo nano /etc/mustask/mustask.env        # Keycloak__ClientSecret
sudo bash mustask-v0.1.0/kit/install.sh nginx --host mustask.example.com --cert <…> --key <…>

# VM, каждый релиз (разделы 8, 9)
sudo bash mustask-v0.1.0/kit/install.sh deploy mustask-v0.1.0
sudo bash mustask-v0.1.0/kit/smoke.sh
```

Источник истины для unit, сайта и ключей env — файлы кита; разделы ниже
объясняют, что делает каждый шаг и зачем.

После первого развёртывания руками раздел 11 превращает раздел 9 в пуш тега.
Self-hosted раннер GitHub Actions на VM развёртывает тег после вашего одобрения.

**Две схемы.** MUSTASK отдаётся либо в корне собственного имени хоста
(`https://mustask.example.com`), либо под путём на хосте, у которого уже есть
сервер (`https://portal.example.com/mustask`, ADR-0037). Путь — одна настройка:
релиз собирается для полного адреса (раздел 4), API получает тот же путь как
`Hosting__PathBase` (раздел 5), а nginx передаёт его без изменений (раздел 7).
Тот же порядок действий под путём:

```bash
# машина сборки
deploy/vm/build-release.sh v0.1.0 https://portal.example.com/mustask

# VM, один раз
sudo bash mustask-v0.1.0/kit/install.sh prepare --host portal.example.com --path /mustask \
    --keycloak-host keycloak.example.com --realm <realm> --install-packages --create-db
sudo nano /etc/mustask/mustask.env        # Keycloak__ClientSecret
sudo bash mustask-v0.1.0/kit/install.sh nginx --path /mustask   # печатает строку include
```

Эту одну строку `include` добавляет тот, кто владеет сервером портала
(раздел 7.2). Одно развёртывание обслуживает один путь; перенос — это
пересборка панели и правка env-файла.

Runbook рассчитан на релиз, в котором есть tech-024 (роли клиента) и tech-025
(справочник пользователей заполняется при входе, ADR-0034). На релизе без них
настройка Keycloak другая.

## 1. Что запросить у администратора Keycloak

Всё из этого раздела один раз делает тот, кто администрирует общий realm.
Ничего, кроме двух клиентов и четырёх ролей, не трогается. `<realm>` ниже — имя
уже существующего realm компании; API видит его только как последний сегмент
`Keycloak__Authority`.

### Клиент `mustask-api` (ресурсный сервер)

- Протокол OpenID Connect, client authentication **включено**, поведение
  **bearer-only**: standard flow, direct access grants, implicit flow и
  service accounts — все **выключены**. Он никого не логинит; на нём хранятся
  роли, и он — audience токена.
- Ровно четыре **роли клиента**, написанные именно так:

  | Роль клиента | Что даёт |
  |---|---|
  | `user` | Вход в систему вообще и чтение справочников. **Без неё сессии нет**, что бы ещё у пользователя ни было. |
  | `organization-creator` | Создание организации. |
  | `dictionaries-admin` | Управление справочниками. |
  | `system-admin` | Администрирование экземпляра. |

- Никаких realm-ролей, групп, ролей по умолчанию и «сырых» кодов прав.
  Соответствие этих имён правам задано в коде.
- **Роль `user` назначается напрямую каждому, кто будет пользоваться
  MUSTASK** (самому пользователю, не через группу и не через роль по
  умолчанию). Остальные роли добавляются сверху по необходимости.
- **Первый администратор** — существующая учётная запись сотрудника в realm:
  отдельный аккаунт для MUSTASK не создаётся. Назовите этого человека в
  запросе, и администратор Keycloak назначит ему напрямую **все четыре** роли
  клиента `mustask-api` (`user`, `organization-creator`, `dictionaries-admin`,
  `system-admin`). После первого входа он создаёт первую организацию и
  добавляет в неё остальных; больше никому `organization-creator` не нужна,
  пока не понадобится ещё одна организация.

### Клиент `mustask-panel` (клиент входа)

- Протокол OpenID Connect, **confidential** (client authentication включено).
  Администратор передаёт сгенерированный **client secret** тому, кто
  разворачивает, по закрытому каналу.
- Standard flow **включён**; implicit flow, direct access grants и service
  accounts **выключены**. Метод PKCE — `S256`.
- **Valid redirect URI:** `https://mustask.example.com/api/session/callback`
  (ровно этот, без масок). **Web origins:** пусто.
- **Valid post-logout redirect URI:** `https://mustask.example.com/*`.
  **Root URL:** `https://mustask.example.com`.
- **Под путём на общем хосте** (`https://portal.example.com/mustask`,
  ADR-0037) те же адреса содержат путь:
  - root URL `https://portal.example.com/mustask`;
  - valid redirect URI `https://portal.example.com/mustask/api/session/callback`;
  - valid post-logout redirect URI `https://portal.example.com/mustask/*`;
  - web origins: пусто, а если политика realm требует значение — голый хост
    `https://portal.example.com`, никогда не с путём (у origin пути нет).
- Default client scopes: `basic`, `acr`, `profile`, `email`, `roles`,
  `web-origins`. `profile` и `email` обязательны: при входе MUSTASK
  записывает имя и e-mail пользователя из claims токена (ADR-0034).
- Мапперы на этом клиенте (или на client scope, который он использует):
  1. Маппер **Audience**, included client audience `mustask-api`, добавляется
     в **access token** (в ID token не нужно). API отклоняет токены без
     `aud = mustask-api`.
  2. **Роли клиента в `resource_access`**: маппер «client roles»
     (`oidc-usermodel-client-role-mapper`) стандартного scope `roles` должен
     класть роли клиента в **access token** в `resource_access`. API читает
     `resource_access.mustask-api.roles`. Стандартный scope `roles` так и
     делает; проверьте, что claim в access token включён и что маппер не
     ограничен другим client id.

Вход использует pushed authorization requests (PAR) и
`response_mode=form_post`; Keycloak по умолчанию разрешает оба, включать ничего
не нужно — только убедитесь, что для этого клиента ни один не отключён.

Как проверить: в консоли Clients → `mustask-panel` → Client scopes →
Evaluate. Выберите тестового пользователя, сгенерируйте access token и
убедитесь, что в нём есть `"aud"` с `mustask-api`,
`resource_access.mustask-api.roles` с ролями выше, а также имя и `email`.

## 2. VM

Допущения: Linux с systemd (кит отрепетирован на Ubuntu 24.04), root через
`sudo`, порты 80/443 открыты наружу, API слушает только `127.0.0.1:5000`,
PostgreSQL на этой же VM.

Распакуйте релиз (раздел 4) в любое место, например в домашний каталог, и один
раз подготовьте машину:

```bash
tar -xzf mustask-v0.1.0.tar.gz
sudo bash mustask-v0.1.0/kit/install.sh prepare \
    --host mustask.example.com --keycloak-host keycloak.example.com --realm <realm> \
    --install-packages --create-db
```

Под путём на общем хосте добавьте `--path /mustask` и укажите голый хост:
`--host portal.example.com --path /mustask`. Тогда `prepare` запишет
`Session__PanelUrl=https://portal.example.com/mustask` и
`Hosting__PathBase=/mustask` (раздел 5). Путь тот же, для которого собран
релиз (раздел 4); в корне хоста `--path` не указывайте.

`prepare` создаёт служебного пользователя `mustask` без входа в систему
(домашний каталог `/var/lib/mustask`), каталоги `/opt/mustask/releases/` и
`/etc/mustask/` и пишет `/etc/mustask/mustask.env` из шаблона (раздел 5);
существующий env-файл не трогается, поэтому `prepare` можно запускать
повторно. С `--run-as <пользователь>` служба работает от уже
существующей учётной записи — для VM, где все остальные приложения работают
от одной учётной записи (`--run-as ubuntu`): пользователь `mustask` не
создаётся, env-файл отдаётся группе этого пользователя, а `/var/lib/mustask` —
ему самому. Других учётных записей, кроме `mustask`, `prepare` не создаёт, а
root отклоняет. Выбор хранится в `/etc/mustask/run-as`, и `deploy` вписывает
его в `User=` unit-файла. Повторный `prepare` без `--run-as` сохраняет
записанную учётную запись; чтобы сменить её, запустите `prepare` ещё раз с
другим `--run-as` и сделайте deploy. У общей учётной записи есть цена: её
остальные процессы могут читать секреты env-файла и ключи Data Protection,
служба получает группы этой учётной записи (`NoNewPrivileges` закрывает sudo,
но не, например, группу docker), а при `ProtectHome=true` `dotnet` не должен
лежать под `/home`. Два флага — это то, что меняет машину, и каждый задаётся явно:

- `--install-packages` ставит только недостающее из
  `aspnetcore-runtime-10.0`, PostgreSQL, nginx и curl, через
  `apt-get install --no-upgrade`: уже стоящие на VM PostgreSQL и nginx не
  обновляются и не перезапускаются. На Ubuntu 24.04 всё это ставится из
  штатного архива дистрибутива (на репетиции: ASP.NET Core 10.0.14, PostgreSQL 16,
  nginx 1.24); unit запускает `/usr/bin/dotnet`. Не указывайте флаг, если
  пакеты ставятся другим способом.
- `--create-db`: раздел 3.

**VM должна доверять сертификату хоста Keycloak.** API обращается к
`keycloak.example.com` по HTTPS (discovery, ключи подписи, обмен токенов), и
.NET проверяет сертификат по системному хранилищу. Если Keycloak компании
использует внутренний CA, установите этот CA на VM (на Ubuntu: скопируйте его
как `.crt` в `/usr/local/share/ca-certificates/` и выполните
`sudo update-ca-certificates`) до первого deploy; иначе `deploy` остановится в
ожидании `/health`, который покажет realm недоступным.

`/var/lib/mustask` должен сохраняться между перезапусками и обновлениями: API
шифрует cookie сессии ключами ASP.NET Core Data Protection, которые лежат там
(unit задаёт `HOME=/var/lib/mustask`, от какого бы пользователя он ни работал). Если каталог очищен, все открытые
сессии становятся недействительными, и пользователи входят заново.

## 3. База данных

С `--create-db` скрипт `prepare` создаёт на локальном PostgreSQL роль с
логином и принадлежащую ей базу `mustask` под сгенерированным паролем, который
сразу записывается в строку подключения в `mustask.env`. Пароль нигде не
печатается, знать его никому не нужно. Существующая роль остаётся как есть,
если её пароль уже записан в `mustask.env` (иначе `prepare` останавливается и
говорит об этом); недостающая база создаётся. Существующая база `mustask`
берётся как есть, кто бы ею ни владел: проверьте, что это именно она.

Для PostgreSQL на другом сервере создайте их сами и впишите строку в
`mustask.env` вручную:

```sql
CREATE ROLE mustask LOGIN PASSWORD '<надёжный пароль>';
CREATE DATABASE mustask OWNER mustask;
```

API читает строку подключения `ConnectionStrings__mustask` (строка Npgsql),
например
`Host=127.0.0.1;Port=5432;Database=mustask;Username=mustask;Password=<…>`.

Миграции **применяются явно и никогда — при старте API** (ADR-0002 в
репозитории: `docs/adr/0002-explicit-database-migrations.md`). Это делает
`install.sh deploy` бандлом миграций релиза (`efbundle`, собирается вместе с
релизом в разделе 4) до переключения на новый код: на самой VM, со строкой
подключения из `mustask.env`, без SDK, без checkout и без туннеля к базе.
Бандл применяет только те миграции, которых в базе ещё нет, поэтому запуск на
каждом deploy безопасен.

Без кита то же делает `dotnet ef database update --project
src/backend/Mustask.Infrastructure` из checkout на версии релиза; на свежем
checkout сначала выполните `dotnet restore src/backend/Mustask.Infrastructure`,
иначе `dotnet ef` остановится с `NETSDK1004`.

## 4. Сборка

На машине сборки (любая ОС с bash — на Windows подходит Git Bash — плюс git,
.NET 10 SDK, `dotnet-ef` 10.x и Node.js с npm):

```bash
git fetch --tags
dotnet tool install --global dotnet-ef        # один раз на машину сборки
deploy/vm/build-release.sh v0.1.0 https://mustask.example.com
```

Скрипт собирает **тег, а не рабочую копию**: он выкладывает тег во временный
worktree, поэтому локальные правки на VM не попадают, и затем удаляет его.
Второй аргумент — адрес сайта без слеша в конце: только хост
(`https://mustask.example.com`) или хост с путём
(`https://portal.example.com/mustask`; сегменты из букв, цифр, `.`, `_` и
`-`, без порта). Адрес вшивается в панель как `VITE_API_BASE_URL` (панель и API
живут по одному адресу, поэтому это адрес самого сайта), а путь — `/`, если
его нет, — как `VITE_BASE_PATH`, база, под которой отдаётся панель. Адрес
записывается и в `VERSION`, а `install.sh deploy` отказывается принимать
релиз, адрес которого не совпадает с `Session__PanelUrl`. Скрипт также проверяет, что путь действительно попал в сборку панели
(скрипты в `panel/index.html` лежат под ним). В Git Bash он не даёт превратить
путь в путь Windows (`C:/Program Files/Git/mustask`) по дороге к Node; не
задавайте `MSYS_NO_PATHCONV=1` на весь скрипт, иначе `git` не найдёт
репозиторий. Результат — один файл `out/mustask-v0.1.0.tar.gz`:

| Внутри | Что |
|---|---|
| `api/` | `dotnet publish` проекта `Mustask.Api`, framework-dependent: на VM запускается через `dotnet` |
| `panel/` | сборка панели |
| `efbundle` | бандл миграций, linux-x64, self-contained |
| `kit/` | `install.sh`, `smoke.sh`, unit, сайт nginx и сниппет location-блоков nginx, шаблон env и три скрипта раздела 11 (`mustask-cd.sh`, `runner-setup.sh`, `runner-job-guard.sh`) — из checkout, в котором запущен скрипт |
| `VERSION` | тег, коммит, коммит кита, адрес сайта, время сборки |

Скопируйте его на VM и распакуйте:

```bash
scp out/mustask-v0.1.0.tar.gz <vm>:
ssh <vm> tar -xzf mustask-v0.1.0.tar.gz
```

## 5. Конфигурация: переменные окружения

API настраивается только через `/etc/mustask/mustask.env` (владелец root,
права `0640`, группа учётной записи службы: по умолчанию `mustask`), который `prepare` пишет из
`deploy/vm/mustask.env.template`. `__` — так в переменных окружения
записывается `:` ключа конфигурации. `prepare` заполняет адрес сайта, адреса
Keycloak и, с `--create-db`, строку подключения. **Вам остаётся одно значение:
`Keycloak__ClientSecret`** — секрет `mustask-panel` от администратора
Keycloak. Правьте файл на VM (`sudo nano /etc/mustask/mustask.env`); никогда
не вставляйте секрет в чат, тикет или репозиторий. `install.sh deploy`
отказывается работать, пока в какой-то настройке остаётся `CHANGE_ME`.

| Ключ | Значение |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://127.0.0.1:5000`: только loopback, перед ним nginx. Порт не меняйте: его используют сайт nginx и `install.sh` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true`: TLS завершается на прокси; без этого API видит `http://` и строит адреса возврата, не совпадающие с зарегистрированными в Keycloak |
| `ConnectionStrings__mustask` | раздел 3 |
| `Keycloak__Authority` | `https://keycloak.example.com/realms/<realm>`, без слеша в конце |
| `Keycloak__Audience` | `mustask-api` |
| `Keycloak__Issuer` | необязательно, по умолчанию равно authority; задавайте, только если в токенах другой `iss` |
| `Keycloak__ClientSecret` | секрет `mustask-panel`. Вне Development API не стартует, если он короче 32 символов или является известной заглушкой |
| `Session__PanelUrl` | `https://mustask.example.com`, без слеша в конце; под путём на общем хосте содержит путь: `https://portal.example.com/mustask`. Каждый редирект после входа строится как `{PanelUrl}{returnUrl}` |
| `Hosting__PathBase` | путь из `Session__PanelUrl`: `/mustask`, либо пусто в корне хоста. API не запускается, если он не совпадает с путём `PanelUrl` (ADR-0037). `prepare --path` заполняет оба |

Примечания:

- `Keycloak__Authority`, `Keycloak__Audience`, `Keycloak__ClientSecret` и
  `Session__PanelUrl` обязательны: при отсутствии любой из них хост
  останавливается на старте с сообщением, где назван ключ.
  `Keycloak__RequireHttpsMetadata` вне Development по умолчанию включён;
  оставьте так.
- **`Cors__AllowedOrigins__0` в этой схеме не нужен.** `Mustask.Api` включает
  CORS только в Development; при панели и API на одном домене междоменных
  запросов нет. Если когда-нибудь панель будет на другом origin, заголовки
  CORS должен ставить прокси.
- Файл в формате systemd, а не shell: в значениях есть `;`. Не делайте ему
  `source`.
- Секреты хранятся только в этом файле (или в хранилище секретов, которое его
  наполняет), никогда — в репозитории или в `appsettings*.json`.

## 6. Unit systemd

`deploy/vm/mustask-api.service`; `install.sh deploy` ставит его как
`/etc/systemd/system/mustask-api.service` и включает. Он запускает
`/usr/bin/dotnet /opt/mustask/current/api/Mustask.Api.dll` от пользователя
из `/etc/mustask/run-as` (по умолчанию `mustask`, раздел 2), с
env-файлом из раздела 5, `ProtectSystem=strict` и `/var/lib/mustask` как
единственным путём на запись (ключи Data Protection). Если `dotnet`
установлен в другом месте, поправьте `ExecStart` в файле кита до сборки.

```bash
journalctl -u mustask-api -f       # структурированный JSON-лог в консоли
```

## 7. Reverse proxy и TLS (nginx)

Две схемы; выберите ту, что соответствует адресу, для которого собран релиз
(раздел 4).

- **Собственное имя хоста** (`https://mustask.example.com`): у MUSTASK целый
  свой `server {}`. Подраздел 7.1.
- **Путь на общем хосте** (`https://portal.example.com/mustask`): на хосте уже
  есть сервер, и принадлежит он другим; MUSTASK добавляет в него location.
  Подраздел 7.2.

В обеих схемах nginx передаёт путь в API без изменений (`proxy_pass` без
части URI). Сам API работает под своей базой пути (`Hosting__PathBase`,
раздел 5), поэтому по дороге ничего не обрезается и не переписывается.

HTTPS обязателен: cookie сессии `mustask.session` — `Secure`, `HttpOnly`,
`SameSite=Lax`, путь `{путь}/api/session` (`/api/session` в корне хоста), и по
обычному HTTP не отправляется никогда.

### 7.1 Собственное имя хоста

Сайт — `deploy/vm/nginx-mustask.conf`: порт 80 перенаправляет на HTTPS; на
443 `/api/` уходит на `127.0.0.1:5000` с forwarded-заголовками, всё остальное —
панель из `/opt/mustask/current/panel` с возвратом к `index.html`. `/health` и
`/alive` наружу не открыты. Сертификат ваш (компании или certbot); укажите
скрипту его файлы:

```bash
sudo bash mustask-v0.1.0/kit/install.sh nginx --host mustask.example.com \
    --cert /etc/letsencrypt/live/mustask.example.com/fullchain.pem \
    --key  /etc/letsencrypt/live/mustask.example.com/privkey.pem
```

Скрипт пишет `/etc/nginx/sites-available/mustask`, включает сайт, выполняет
`nginx -t` и перезагружает nginx. Если проверка не прошла, возвращается сайт
предыдущего запуска (а при первом запуске новый выключается), так что
сломанный сайт не остаётся включённым до следующей перезагрузки. Две вещи в
сайте всё же касаются остальных сайтов этого nginx: на nginx до 1.25.1
`http2` в строке `listen 443` включает HTTP/2 для всех серверов на этом
порту, а если для 443 нет другого `default_server`, этот сайт отвечает на
имена, которые никто не настраивал. Две настройки в файле стоят по причине,
найденной на репетиции (tech-056):

- `listen 443 ssl http2;` — для nginx, который ставят Ubuntu 24.04 и Debian 12
  (1.24 и 1.22): на `http2 on;` они останавливаются с `unknown directive
  "http2"`. На 1.25.1 и новее форма в `listen` тоже работает и только пишет
  предупреждение об устаревании.
- `proxy_buffer_size 32k` и две строки после неё: callback входа отвечает
  cookie сессии (~3.5 КБ на пустом realm и больше с каждым claim общего realm)
  плюс очищаемыми OIDC-cookie; со стандартным буфером заголовков nginx
  4–8 КБ каждый вход получает 502.

### 7.2 Путь на общем хосте

Сервер для `portal.example.com` (его имена, сертификат, редирект с порта 80)
не принадлежит MUSTASK, и кит его не пишет. Он устанавливает только
location-блоки из
[`deploy/vm/nginx-mustask-locations.conf`](../../deploy/vm/nginx-mustask-locations.conf):

- `location /mustask/api/` уходит на `127.0.0.1:5000` с forwarded-заголовками
  и теми же тремя настройками буферов прокси, что и в 7.1 (причина та же: без
  них каждый вход получает 502);
- `location /mustask/` отдаёт панель из `/opt/mustask/current/panel/` через
  `alias`, а неизвестный путь возвращается к `/mustask/index.html`; `/mustask`
  без слеша перенаправляет на `/mustask/`;
- `/mustask/health` наружу не открыт: под префиксом это `index.html` панели,
  как и в корне. `/health` и `/alive` остаются на loopback.

```bash
sudo bash mustask-v0.1.0/kit/install.sh nginx --path /mustask
```

Здесь нет `--host`, `--cert`, `--key` и `default_server`: скрипт кладёт
location-блоки в `/etc/nginx/snippets/mustask-locations.conf`, проверяет их
`nginx -t` в отдельном временном сервере (пока их никто не подключает),
проверяет всю текущую конфигурацию и печатает одну строку, которую владелец
сервера портала должен добавить внутрь своего блока `server { … }`:

```nginx
include /etc/nginx/snippets/mustask-locations.conf;
```

**Владелец сервера портала должен согласиться на этот include.** Это
организационный шаг развёртывания: запросите его вместе с запросом к Keycloak
из раздела 1. После добавления `nginx -t && systemctl reload nginx` включает
блоки. Повторный запуск скрипта сам перезагрузит nginx, когда include уже
есть. Если не прошла любая из двух проверок, ранее установленный сниппет
восстанавливается (или новый удаляется), так что сломанный сниппет не
остаётся активным.

Location-блоки `/mustask` — префиксные с `^~`, поэтому regex-location сервера
портала (`location ~* .js$`) не перехватывает их пути; собственный
префиксный location портала на `/mustask` по-прежнему конфликтовал бы с
include.

**TLS завершается на прокси перед порталом.** По умолчанию API получает
`X-Forwarded-Proto $scheme`, то есть схему запроса, который пришёл в сам nginx.
Если публичный TLS завершается на внешнем прокси, а до этого nginx запрос идёт
по обычному http, там будет `http`: API построит адреса редиректа входа с
`http://`, и Keycloak их отклонит. Укажите схему, которая на самом деле у
публичного адреса:

```bash
sudo bash mustask-v0.1.0/kit/install.sh nginx --path /mustask --forwarded-proto https
```

Выбор сохраняется в `/etc/mustask/forwarded-proto`, поэтому следующий запуск
без флага (обновление кита) оставит `https`. `--forwarded-proto scheme`
возвращает значение по умолчанию. Флаг только для `--path`: сайт из 7.1 сам
завершает TLS, и там `$scheme` верен.

## 8. Первый запуск и smoke-проверка

```bash
sudo bash mustask-v0.1.0/kit/install.sh deploy mustask-v0.1.0
sudo bash mustask-v0.1.0/kit/smoke.sh
```

`deploy` копирует релиз в `/opt/mustask/releases/v0.1.0`, применяет миграции
(раздел 3), ставит unit, направляет `/opt/mustask/current` на релиз,
перезапускает службу и до минуты ждёт `/health`. Если он так и не стал
здоров, `deploy` печатает последние строки лога службы и останавливается; по
логу видно, проблема в базе или в Keycloak.

`smoke.sh` проверяет с VM: `/health` отвечает `Healthy` (проверка охватывает
базу и доступность realm); `http://` перенаправляет на `https://`; панель
отдаётся по адресу сайта, её первый скрипт отдаётся по пути, который называет
`index.html` (панель, собранная для другой базы пути, показывает пустую
страницу), а клиентский маршрут возвращает `index.html`; API
отвечает через прокси (401 без токена); `/api/session/login` уводит на realm;
`/health` через прокси недоступен. Под путём каждый адрес — адрес сайта с его
путём (`https://portal.example.com/mustask/…`), а проверка `http://` ждёт
редирект от собственного сервера портала на порту 80. Пока DNS не указывает на
VM, добавьте
`--resolve-local`; `--insecure` принимает сертификат, которому VM не доверяет
(только для репетиций).

Остальное требует человека с браузером; `smoke.sh` печатает это списком:

1. `https://mustask.example.com/` (под путём —
   `https://portal.example.com/mustask/`) показывает панель; вход уводит на
   `keycloak.example.com` и после логина возвращает в панель.
2. Пользователь с `user` входит и попадает в панель. Пользователь **без**
   `user` возвращается с `authError=no_access`, и сессии у него нет.
3. Роли работают: пользователь только с `user` открывает справочники, но
   создать организацию ему отказано («Недостаточно прав»); первый
   администратор (все четыре роли, раздел 1) создаёт первую организацию. Если
   отказано и администратору, выполните для него проверку Evaluate из
   раздела 1: в `resource_access.mustask-api.roles` должны быть все четыре
   роли.
4. После первого входа пользователь находится в поиске участников (справочник
   заполняется при входе, импорта нет): создатель организации находит другого
   тестового пользователя по имени или e-mail при добавлении участника.

## 9. Обновление и откат

Соберите новый тег (раздел 4), скопируйте на VM, затем:

```bash
sudo bash mustask-v0.2.0/kit/install.sh deploy mustask-v0.2.0
sudo bash mustask-v0.2.0/kit/smoke.sh
```

`deploy` применяет новые миграции, переключает `current` на новый релиз и
сохраняет заменённый как `/opt/mustask/previous`, если тот хоть раз был
здоров: deploy исправления поверх неудачного релиза оставляет целью отката
последний хороший. Deploy уже установленного
тега заменяет его на месте.

```bash
sudo bash mustask-v0.2.0/kit/install.sh rollback    # current <-> previous, перезапуск
sudo bash mustask-v0.2.0/kit/install.sh status      # активный VERSION и состояние службы
```

**Откат не откатывает миграции.** Предыдущий код работает с более новой
схемой, и это работает, пока миграции аддитивные; релизу с разрушающей
миграцией нужна своя инструкция по откату. Старые релизы остаются в
`/opt/mustask/releases/`; ненужные удаляйте вручную (никогда не `current` и не
`previous`).

## 10. Диагностика

| Симптом | Вероятная причина |
|---|---|
| `build-release: dotnet-ef is missing` | `dotnet tool install --global dotnet-ef` (раздел 4). |
| `install: … still has CHANGE_ME on line(s) …` | В `mustask.env` осталось значение шаблона: обычно `Keycloak__ClientSecret` (раздел 5). |
| `install: the release is built for … but Session__PanelUrl is …` | Архив собран для другого адреса сайта: пересоберите его с адресом этой VM (раздел 4). |
| `install: nginx -t failed; the site is written but not enabled` | nginx не может загрузить файл сертификата или ключа; `nginx -t` выше называет строку. |
| `install: cannot reach the local PostgreSQL` | PostgreSQL не запущен: `systemctl status postgresql`. |
| `deploy` останавливается: `/health did not turn healthy in 60 s` | Причину называет лог, напечатанный выше: строка подключения к базе или realm (следующие строки). `install.sh rollback` возвращает предыдущий релиз, если он есть. |
| API падает на старте, называя ключ `Keycloak:*` или `Session:PanelUrl` | В `mustask.env` нет переменной. |
| API падает на старте из-за секрета «known placeholder or shorter than 32» | `Keycloak__ClientSecret` — не настоящий секрет `mustask-panel`. |
| `nginx -t`: `unknown directive "http2"` | nginx старше 1.25.1 и сайт правили вручную: оставьте `listen 443 ssl http2;` (раздел 7). |
| После логина в Keycloak браузер получает **502** на `/api/session/callback`; в error-логе nginx «upstream sent too big header» | Нет буферов прокси из раздела 7: cookie сессии не помещается в стандартный буфер заголовков nginx. |
| `/health` нездоров и называет realm; в логе ошибка TLS / сертификата | VM не доверяет CA сертификата Keycloak (раздел 2). |
| `dotnet ef` падает с `NETSDK1004` (без кита) | Сначала выполните `dotnet restore src/backend/Mustask.Infrastructure` (раздел 3). |
| API падает на старте из-за `Hosting:PathBase` | Путь в `Session__PanelUrl` и `Hosting__PathBase` расходятся, либо база записана неверно (раздел 5). Под путём в обоих он есть: `https://portal.example.com/mustask` и `/mustask`; в корне хоста `Hosting__PathBase` пуст. |
| `install: the snippet failed nginx -t` (с `--path`) | Сниппет не прошёл `nginx -t` в отдельном временном сервере; сообщение выше называет строку. Сниппет, установленный раньше, остаётся как был. |
| `install: nginx -t fails on the current configuration` (с `--path`) | Конфигурация портала не проходит с новым сниппетом; прежний сниппет восстановлен (или новый удалён). Сообщение выше называет строку. |
| Под путём: панель открывается, но её скрипты дают 404, или вход зацикливается | Панель собрана для другого пути, чем отдаёт nginx, либо в сервере портала нет `include` (раздел 7). Пересоберите с полным адресом (раздел 4). |
| Keycloak показывает «Invalid redirect URI» | У `mustask-panel` нет `https://mustask.example.com/api/session/callback` (под путём — `https://portal.example.com/mustask/api/session/callback`), либо не задан `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, и API отправил адрес `http://`, либо TLS завершается на прокси перед порталом, а сниппет установлен без `--forwarded-proto https` (раздел 7.2). |
| После входа каждый вызов API — 401 | В access token нет `aud = mustask-api`, или `Keycloak__Authority`/`Keycloak__Issuer` не совпадают с `iss` токена. |
| Вход возвращается с `authError=no_access` | У пользователя нет роли клиента `mustask-api` `user` (назначьте напрямую). |
| Вход есть, но роль не действует | В access token нет `resource_access.mustask-api.roles` (маппер, раздел 1). |
| После перезапуска всех разлогинило | `/var/lib/mustask` (ключи Data Protection) не сохраняется или недоступен службе на запись. |
| Запуск деплоя: `the repository variable MUSTASK_SITE_URL is not set` | Раздел 11.1, пункт 3. |
| Job деплоя ждёт «a runner … with labels self-hosted, linux, mustask-prod» | Раннер офлайн: `systemctl status 'actions.runner.*'` на VM и Settings → Actions → Runners (раздел 11.2). |
| Запуск деплоя: `sudo: a password is required` | `runner-setup.sh` на этой VM не запускался или `/etc/sudoers.d/mustask-runner` пропал: запустите его снова (раздел 11.2). |
| Запуск деплоя: `mustask-cd: refused: …` | Артефакт — не один релиз от `build-release.sh`; сообщение говорит, что не так. Ничего не установлено. |
| Запуск деплоя: `mustask-cd: refused: /etc/mustask/cd-smoke-args` | В файле что-то кроме `--resolve-local` / `--insecure`: перезапустите `runner-setup.sh --smoke-args "…"`. |
| Job падает в «Set up runner» с `mustask runner guard: this job may not run on the production runner` | Так и задумано для всего, кроме `deploy.yml` из `main` или тега `v*` (раздел 11.4). Если это настоящий деплой, проверьте, что `/etc/mustask/cd-repository` называет этот репозиторий (он меняется, если репозиторий переименован или перенесён: перезапустите `runner-setup.sh` с новым `--url`). |

## 11. Непрерывное развёртывание (GitHub Actions)

Когда разделы 1–8 один раз пройдены руками, релизный тег может
развёртываться сам.
[`.github/workflows/deploy.yml`](../../.github/workflows/deploy.yml) собирает
релиз на раннере GitHub ровно так же, как раздел 4, и ждёт одобрения. После
одобрения self-hosted раннер на этой VM выполняет раздел 9:

```
git push origin v0.2.0
  └─ build  (ubuntu-latest): build-release.sh v0.2.0 $MUSTASK_SITE_URL → артефакт + sha256
  └─ deploy (эта VM, метка mustask-prod, окружение production → одобрение):
        проверка sha256 → sudo -n mustask-cd deploy < mustask-v0.2.0.tar.gz   (install.sh deploy)
                        → sudo -n mustask-cd smoke                             (smoke.sh)
```

На VM не нужны ни .NET SDK, ни Node, ни клон репозитория: туда приходит
только архив. Раннер работает от непривилегированного пользователя
`mustask-runner`, которому от root разрешены ровно две команды:
`mustask-cd deploy` (архив через stdin) и `mustask-cd smoke`. `mustask-cd` —
это [`deploy/vm/mustask-cd.sh`](../../deploy/vm/mustask-cd.sh), его ставит
[`deploy/vm/runner-setup.sh`](../../deploy/vm/runner-setup.sh). Репозиторий
публичный, поэтому порядок ниже важен: сначала настройки репозитория, потом
раннер.

### 11.1 Настройки репозитория (один раз, владелец репозитория)

Всё это — в **Settings** репозитория.

1. **Actions → General → Approval for running fork pull request workflows
   from contributors:** «Require approval for all external contributors».
   Тогда PR из форка ничего не запускает без вашего одобрения. Одобрение
   запускает workflow *в том виде, в каком они лежат в PR*, поэтому PR может
   добавить workflow, который просит этот раннер. Охранник job на VM (11.4)
   роняет такой job до первого шага. И всё же читайте каждый PR, который
   трогает `.github/`, прежде чем одобрять его запуск.
2. **Environments → New environment `production`:**
   - *Required reviewers*: вы сами. «Prevent self-review» не включайте: тег
     пушите и одобряете вы же.
   - *Deployment branches and tags*: «Selected branches and tags». Добавьте
     правило для ветки `main` и правило для тегов `v*`. Запуск из любой
     другой ветки до job деплоя не дойдёт.
3. **Secrets and variables → Actions → Variables → New repository variable**
   `MUSTASK_SITE_URL`: публичный адрес, символ в символ совпадающий с
   `Session__PanelUrl` из `/etc/mustask/mustask.env`. Примеры:
   `https://mustask.example.com`, `https://portal.example.com/mustask`.
   `install.sh deploy` отказывает релизу, собранному под другой адрес.
   Секреты не нужны: ничего секретного с VM не уходит.
4. **Rules → Rulesets → New ruleset → New tag ruleset:** цель `v*`,
   enforcement Active. Включите «Restrict creations», «Restrict updates» и
   «Restrict deletions», а в bypass-список добавьте Repository admin. Тогда
   создать или передвинуть релизный тег можете только вы.
5. **Группы раннеров.** У репозитория, принадлежащего пользователю (как этот),
   групп нет, и его раннер обслуживает только этот репозиторий. Здесь работу
   группы делает охранник job на VM (11.4). Если репозиторий
   переедет в организацию, вынесите раннер в отдельную группу. Дайте этой группе
   доступ только к этому репозиторию, включите «Allow public repositories» и
   задайте «Restrict to selected workflows» =
   `<org>/mustask/.github/workflows/deploy.yml@refs/heads/main`.

### 11.2 Раннер на VM (один раз, root)

Три скрипта (`runner-setup.sh`, `mustask-cd.sh`, `runner-job-guard.sh`)
лежат в `deploy/vm/` репозитория и в `kit/` любого релиза, где они уже есть.
Держите их рядом. На VM:

```bash
sudo bash runner-setup.sh --url https://github.com/totKtoXam/mustask \
    --runner-version 2.338.2 \
    --runner-sha256 baebf204307bac6193480852732f0aba2ae97b6c5597c244f4d5558390147c40
```

- `--runner-version` и `--runner-sha256` берутся со страницы
  [github.com/actions/runner/releases](https://github.com/actions/runner/releases),
  из строки `actions-runner-linux-x64-<версия>.tar.gz`. Значения выше —
  последние на 2026-11-10. Если контрольная сумма скачанного не совпадёт,
  скрипт откажет.
- Скрипт спросит **регистрационный токен**. Возьмите его в Settings → Actions →
  Runners → New self-hosted runner → Linux: это значение после `--token` в
  строке `./config.sh`. Токен действует час. При вводе он не отображается и
  не попадает ни в командную строку, ни в файл.
- Добавьте `--smoke-args --resolve-local`, если VM не достучится до своего
  публичного имени. Это тот же флаг, что `smoke.sh --resolve-local` в разделе
  8. `--insecure` нужен, пока VM не доверяет сертификату. Флаги
  сохраняются в `/etc/mustask/cd-smoke-args`.

Что делает скрипт:

- Создаёт системного пользователя `mustask-runner` с домом
  `/var/lib/mustask-runner`. У него нет пароля и login-shell, и он не входит
  в группы `sudo`, `docker`, `lxd`. Если он уже в такой группе, скрипт
  откажет.
- Ставит `/usr/local/sbin/mustask-cd` (root, 0755).
- Пишет `/etc/sudoers.d/mustask-runner` (root, 0440) и перед установкой
  проверяет его `visudo`:
  `mustask-runner ALL=(root) NOPASSWD: /usr/local/sbin/mustask-cd deploy, /usr/local/sbin/mustask-cd smoke`.
- Ставит раннер в `/var/lib/mustask-runner/actions-runner`, вместе с его
  собственным списком пакетов (`installdependencies.sh`, apt). Регистрирует
  раннер с меткой `mustask-prod` и ставит его как сервис systemd
  (`actions.runner.totKtoXam-mustask.<имя>.service`) от имени
  `mustask-runner`.
- Ставит охранник job (11.4): `runner-job-guard.sh` как
  `/usr/local/libexec/mustask-runner/job-started.sh` (root, 0755), имя
  репозитория в `/etc/mustask/cd-repository` (root, 0644) и drop-in systemd
  `/etc/systemd/system/actions.runner.<…>.service.d/mustask-job-guard.conf`
  (root, 0644). Drop-in задаёт `ACTIONS_RUNNER_HOOK_JOB_STARTED` равным пути
  охранника. Если drop-in новый или изменился, скрипт перезапускает сервис.

Проверка:

```bash
sudo -u mustask-runner sudo -n -l     # ровно две команды mustask-cd
systemctl status 'actions.runner.*'   # active (running)
systemctl cat 'actions.runner.*'      # в конце mustask-job-guard.conf
```

На GitHub в Settings → Actions → Runners раннер виден как Idle, с метками
`self-hosted`, `Linux`, `X64` и `mustask-prod`.

Повторный запуск `runner-setup.sh` на зарегистрированном раннере регистрацию
не трогает. Он обновляет обёртку, охранник, sudoers и флаги smoke и ставит
сервис, если прошлый запуск до него не дошёл. Без `--smoke-args` записанные
флаги сохраняются. Запускайте его снова, когда меняется `mustask-cd.sh` или
`runner-job-guard.sh`: деплой их не заменяет. Если drop-in изменился,
повторный запуск перезапускает сервис раннера, поэтому не запускайте его во
время деплоя.

### 11.3 Выпуск, повторный деплой, откат

```bash
git tag v0.2.0 && git push origin v0.2.0
```

Дальше Actions → **Deploy** → запуск → «Review deployments» → `production`
→ Approve. Job деплоя печатает вывод `install.sh deploy` и `smoke.sh`.
Проваленная smoke-проверка роняет запуск. Чек-лист в браузере, который
печатает `smoke.sh`, остаётся за вами (раздел 8).

Чтобы снова развернуть существующий или более старый тег: Actions → Deploy →
Run workflow, ветка `main`, tag `v0.1.0`. Старый тег, развёрнутый так,
запускает свой бандл миграций, а на более новой схеме тот ничего не
применяет. Быстрый откат — по-прежнему `install.sh rollback` на VM (раздел
9): его нет в CI, и у раннера нет права его запускать.

Два запуска не пересекаются. Второй ждёт окончания первого и никогда не
отменяется посреди `install.sh deploy`. Но ждать может только один: третий
запуск вытесняет ждущий второй, и тот отменяется, не начавшись. Пушьте
релизные теги по одному. Если запуск так отменился, запустите его снова через
Run workflow.

### 11.4 Модель доверия

Кто может запустить job деплоя, тот может выполнить код от root на этой VM.
Это не изъян раннера: `install.sh deploy` и бандл миграций релиза и руками
выполняются от root. Поэтому job огорожен:

- **VM выполняет только наш job деплоя.** Какой раннер нужен, решает файл
  workflow, а pull request запускает файлы workflow *из самого PR*. Поэтому
  файлы репозитория не могут удержать PR от раннера. Это делает сама VM:
  перед каждым job раннер запускает охранник (`job-started.sh`, из
  принадлежащего root drop-in из 11.2). Охранник пускает job, только если
  выполнено всё сразу:
  - репозиторий — тот, что в `/etc/mustask/cd-repository`;
  - событие — `push` или `workflow_dispatch`;
  - workflow — `.github/workflows/deploy.yml` этого репозитория, из `main`
    или тега `v*`.

  Всё остальное, включая pull request, падает до первого шага с
  `mustask runner guard: …`. Отсутствующая переменная — тоже отказ. Чтобы
  запушить тег или запустить workflow вручную, нужен доступ на запись в
  репозиторий, которого у форка нет. Охранник и drop-in принадлежат root.
  Каталог самого раннера принадлежит `mustask-runner`, и `.env` в нём мог бы
  выключить хук при следующем перезапуске. Но от этого пользователя работает
  только код, уже прошедший охранник, так что форку это не лазейка. И всё же
  загляните в этот `.env`, если охранник вдруг перестанет отказывать. `deploy.yml`
  запускается только на `push` тегов `v*` и на `workflow_dispatch`. В CI
  `scripts/tests/cd-runner-cases.sh` проверяет это и то, что никакой другой
  workflow не называет раннер. Эти проверки ловят только наши собственные
  ошибки; враждебный PR останавливает охранник.
- **Релизные теги создаёте только вы** — через ruleset тегов (11.1, пункт 4).
- **Каждый деплой ждёт вашего одобрения**, и только из `main` или тега `v*` —
  это окружение `production` (11.1, пункт 2).
- **Job на VM не выполняет кода репозитория.** У него нет ни checkout, ни
  секретов. Он берёт архив у job сборки и сверяет его sha256. Обёртка
  принимает только один каталог `mustask-<tag>/`, в котором `VERSION` называет
  тот же тег, и распаковывает его в новый каталог, принадлежащий root.
- Actions в `deploy.yml` закреплены по коммиту.

Никогда не делайте так:

- Не добавляйте `pull_request`, `pull_request_target`, `workflow_run` или
  `issue_comment` в workflow, у которого есть job на этом раннере.
- Не давайте `mustask-runner` других правил sudo и групп.
- Не регистрируйте раннер этой VM для другого репозитория.

Артефакт сборки публичного репозитория может скачать любой, кто вошёл в
GitHub. В архиве нет ничего секретного: все секреты живут только в
`mustask.env` на VM.

### 11.5 Удаление раннера

Settings → Actions → Runners → раннер → Remove покажет токен удаления. На VM:

```bash
cd /var/lib/mustask-runner/actions-runner
sudo ./svc.sh stop && sudo ./svc.sh uninstall
sudo -u mustask-runner ./config.sh remove     # спросит токен удаления
sudo rm /etc/sudoers.d/mustask-runner /usr/local/sbin/mustask-cd /etc/mustask/cd-smoke-args \
    /etc/mustask/cd-repository
sudo rm -r /usr/local/libexec/mustask-runner /etc/systemd/system/actions.runner.*.service.d
sudo systemctl daemon-reload
```

## Что этот runbook не проверяет

Разделы 1–8 впервые пройдены вручную 2026-11-05 (tech-056) на чистой Ubuntu
24.04 с systemd вместо VM, против отдельного Keycloak 26.4, realm которого
настроен ровно так, как просит раздел 1; та репетиция также входила скриптом
через настоящую форму логина и проверила роли и первого администратора. В тот
же день так же, на другой чистой Ubuntu 24.04, пройден кит (tech-058): релиз
`v0.1.0` (`8aed774b`) собран на машине с Windows, затем `prepare` с обоими
флагами, CA, секрет вручную, `nginx`, `deploy`, `smoke.sh` (прошёл), второй
`deploy`, `rollback` и повторный deploy того же тега. Записи — в
`docs/features/tech-056-vm-runbook-rehearsal/status.md` и
`docs/features/tech-058-vm-deploy-kit/status.md`.

Не проверено до сих пор, потому что на репетиции этого быть не может:

- **Сеть и realm компании.** Настоящие DNS-имена, цепочка сертификатов
  компании, межсетевые экраны между VM и Keycloak, и realm, где уже есть
  собственные scopes, мапперы, политики, identity providers и required
  actions компании. Любое из этого может изменить токен (и его размер — см.
  буферы прокси в разделе 7) или процесс входа.
- **Вход через браузер.** Сертификаты репетиций были самоподписанными, поэтому
  вход проходил через форму логина скриптом, а не браузером.
- **Обновление с новой миграцией.** Обновление отрепетировано той же сборкой
  под вторым тегом, так что её бандлу нечего было применять.
- **Собственный nginx портала.** Схема под путём (tech-057, ADR-0037)
  отрепетирована 2026-11-05 на чистой Ubuntu 24.04 с systemd, против Keycloak
  26.4, realm которого настроен по разделу 1 для
  `https://portal.t057.localhost/mustask`: релиз собран для этого адреса,
  `prepare --path`, `nginx --path`, одна строка `include` в блоке server,
  изображающем портал, `deploy`, `smoke.sh` прошёл, а скриптовые входы
  (администратор, только `user`, без роли) вели себя так, как описывает раздел
  8, включая путь cookie `/mustask/api/session`. Чего репетиция дать не может —
  настоящего сервера портала (его regex-location и собственные ограничения) и
  согласия его владельца на `include`.

- **Непрерывное развёртывание на GitHub (раздел 11).** 2026-11-12 (tech-066)
  в чистом контейнере Ubuntu 24.04 прошёл `runner-setup.sh`, с раннером,
  помеченным как зарегистрированный: пользователь, обёртка, sudoers с
  проверкой `visudo`, флаги smoke, повторный запуск. Затем контейнер запустил
  `sudo -n` от имени `mustask-runner`. Две команды `mustask-cd` выполнили от
  root `install.sh` релиза (заглушку) и `smoke.sh`. Всё остальное получило
  отказ: другая подкоманда, лишний аргумент, `bash`, сам `install.sh`, `-E`,
  другой целевой пользователь. Охранник job и его drop-in поставлены с
  заглушками `systemctl` и `svc.sh`. Сервис перезапускался только при
  изменении drop-in или остановленном unit. Охранник от имени
  `mustask-runner` пустил push тега и ручной запуск `deploy.yml` и отказал
  pull request, другому workflow и job без переменных. Не пройдены:
  настоящая регистрация, настоящий сервис раннера и то, передаёт ли он
  охраннику эти переменные, и запуск `deploy.yml` с одобрением
  `production`. Эта проверка — первый тег после настройки. Также один раз
  запушьте ветку с тестовым workflow, который называет `mustask-prod`: он
  должен упасть в «Set up runner».

Первое настоящее развёртывание должно снова пройти раздел 8 и исправить этот
документ там, где он расходится с реальностью.
