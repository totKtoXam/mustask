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
PostgreSQL — отдельный сервис на этой же VM или на другом сервере.

```
браузер ──https──> nginx (mustask.example.com)
                     ├─ /        → /var/www/mustask (сборка панели)
                     └─ /api/    → http://127.0.0.1:5000 (Mustask.Api, systemd)
Mustask.Api ──> PostgreSQL            Mustask.Api ──https──> общий Keycloak
```

Ниже `mustask.example.com` означает ваш домен, `keycloak.example.com` — хост
общего Keycloak.

**MUSTASK должен занимать корень своего имени хоста.** Текущий релиз нельзя
опубликовать под под-путём вроде `https://portal.example.com/mustask`: бандл
панели и её маршруты, пути API, путь cookie сессии и адреса возврата после
входа рассчитаны на `/`, и переписывание на прокси этого не исправляет.
Поддержка под-пути — отдельная задача (tech-057); пока она не выпущена, дайте
MUSTASK собственное имя хоста (например, `mustask.example.com`).

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

Допущения (пути можно менять): Linux с systemd, служебный пользователь
`mustask` без входа в систему, порты 80/443 открыты наружу, API слушает только
`127.0.0.1:5000`.

Установить:

- **ASP.NET Core runtime .NET 10** (SDK — на машине, где идёт сборка);
- сам PostgreSQL, если он на этой VM, или клиентские утилиты, если он на
  другом сервере;
- nginx (любой reverse proxy настраивается так же) и сертификат для
  `mustask.example.com` (например, через certbot).

На Ubuntu 24.04 все три ставятся из штатного архива дистрибутива, без
дополнительных репозиториев (на репетиции: ASP.NET Core 10.0.14,
PostgreSQL 16, nginx 1.24); `dotnet` ставится в `/usr/lib/dotnet`, а
`/usr/bin/dotnet` — ссылка на него, его и запускает unit из раздела 6:

```bash
sudo apt-get install -y aspnetcore-runtime-10.0 postgresql nginx
```

**VM должна доверять сертификату хоста Keycloak.** API обращается к
`keycloak.example.com` по HTTPS (discovery, ключи подписи, обмен токенов), и
.NET проверяет сертификат по системному хранилищу. Если Keycloak компании
использует внутренний CA, установите этот CA на VM (на Ubuntu: скопируйте его
как `.crt` в `/usr/local/share/ca-certificates/` и выполните
`sudo update-ca-certificates`) до первого запуска; иначе `/health` в разделе 8
покажет realm недоступным.

```bash
sudo useradd --system --create-home --home-dir /var/lib/mustask --shell /usr/sbin/nologin mustask
sudo mkdir -p /opt/mustask/api /var/www/mustask /etc/mustask
```

`/var/lib/mustask` должен сохраняться между перезапусками и обновлениями: API
шифрует cookie сессии ключами ASP.NET Core Data Protection, которые по
умолчанию лежат в домашнем каталоге служебного пользователя. Если каталог
очищен, все открытые сессии становятся недействительными, и пользователи входят
заново.

## 3. База данных

Создайте пустую базу и владеющий ею логин (для PostgreSQL на самой VM:
`sudo -u postgres psql`):

```sql
CREATE ROLE mustask LOGIN PASSWORD '<надёжный пароль>';
CREATE DATABASE mustask OWNER mustask;
```

API читает строку подключения `ConnectionStrings__mustask` (строка Npgsql),
например
`Host=127.0.0.1;Port=5432;Database=mustask;Username=mustask;Password=<…>`.

Миграции **применяются явно и никогда — при старте API** (ADR-0002 в
репозитории: `docs/adr/0002-explicit-database-migrations.md`; см. также
`docs/runbooks/local-migrations.md`, раздел «External database»). Применяйте их
с машины, где есть .NET 10 SDK, репозиторий на версии разворачиваемого релиза и
сетевой доступ к базе:

```bash
dotnet tool install --global dotnet-ef        # один раз на машину сборки
dotnet restore src/backend/Mustask.Infrastructure
export ConnectionStrings__mustask='Host=<хост БД>;Port=5432;Database=mustask;Username=mustask;Password=<…>'
dotnet ef database update --project src/backend/Mustask.Infrastructure
```

`dotnet restore` на свежем checkout обязателен: без него `dotnet ef`
останавливается с `NETSDK1004` (`project.assets.json` не найден), не дойдя до
базы.

PostgreSQL, поставленный на VM из пакета, слушает только `127.0.0.1`, поэтому
машина сборки напрямую до него не достаёт. Не открывайте его ради этого в сеть;
пробросьте порт по SSH на время команды и направьте строку на локальный конец:

```bash
ssh -N -L 15432:127.0.0.1:5432 <vm> &      # остановить после update
export ConnectionStrings__mustask='Host=127.0.0.1;Port=15432;Database=mustask;Username=mustask;Password=<…>'
```

Выполняйте это перед первым запуском и перед каждым релизом, в котором есть
новая миграция. `dotnet ef migrations bundle` умеет собрать один исполняемый
файл, который запускается на VM без SDK; сейчас репозиторий его не публикует,
так что это вариант, который надо настроить, а не проверенный здесь способ.

## 4. Сборка и установка

На машине сборки, на версии разворачиваемого релиза:

```bash
# API
dotnet publish src/backend/Mustask.Api -c Release -o publish/api

# Панель: VITE_API_BASE_URL вшивается в бандл при сборке, без неё сборка
# падает. Домен один, поэтому это адрес самого сайта (без слеша в конце).
cd src/frontend
npm ci
VITE_API_BASE_URL=https://mustask.example.com npm run build   # результат: dist/
```

Скопируйте `publish/api/` в `/opt/mustask/api/`, а `src/frontend/dist/` — в
`/var/www/mustask/` (старое содержимое заменить; каталог API — владелец root,
доступен `mustask` на чтение). Машина сборки может быть на любой ОС: публикация
framework-dependent, на VM `Mustask.Api.dll` запускается через `dotnet`.
`publish/` не внесён в `.gitignore`; собирайте во временном checkout или
удаляйте его после.

## 5. Конфигурация: переменные окружения

Положите их в `/etc/mustask/mustask.env` (владелец root, права `0640`, группа
`mustask`). `__` — так в переменных окружения записывается `:` ключа
конфигурации.

```ini
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000
# TLS завершается на прокси; без этого API видит http:// и строит адреса
# возврата, не совпадающие с зарегистрированными в Keycloak.
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

ConnectionStrings__mustask=Host=127.0.0.1;Port=5432;Database=mustask;Username=mustask;Password=CHANGE_ME

# Общий realm. Authority — адрес realm, без слеша в конце.
Keycloak__Authority=https://keycloak.example.com/realms/<realm>
Keycloak__Audience=mustask-api
# Необязательно: по умолчанию равно Authority. Задавайте, только если в
# токенах другой iss.
Keycloak__Issuer=https://keycloak.example.com/realms/<realm>
# Secret confidential-клиента mustask-panel (от администратора Keycloak).
# Вне Development API не стартует, если он короче 32 символов или является
# известной заглушкой.
Keycloak__ClientSecret=CHANGE_ME_AT_LEAST_32_CHARACTERS

# Абсолютный адрес панели, без слеша в конце. Каждый редирект после входа
# строится как {PanelUrl}{returnUrl}.
Session__PanelUrl=https://mustask.example.com
```

Примечания:

- `Keycloak__Authority`, `Keycloak__Audience`, `Keycloak__ClientSecret` и
  `Session__PanelUrl` обязательны: при отсутствии любой из них хост
  останавливается на старте с сообщением, где назван ключ.
  `Keycloak__RequireHttpsMetadata` вне Development по умолчанию включён;
  оставьте так.
- **`Cors__AllowedOrigins__0` в этой схеме не нужен.** `Mustask.Api` включает
  CORS только в Development; в Production CORS — забота развёртывания, а при
  панели и API на одном домене междоменных запросов нет. Переменная в
  Production ни на что не влияет. Если когда-нибудь панель будет на другом
  origin, заголовки CORS должен ставить прокси.
- Секреты хранятся только в этом файле (или в хранилище секретов, которое его
  наполняет), никогда — в репозитории или в `appsettings*.json`.

## 6. Unit systemd

`/etc/systemd/system/mustask-api.service`:

```ini
[Unit]
Description=MUSTASK API
After=network-online.target postgresql.service
Wants=network-online.target

[Service]
Type=exec
User=mustask
Group=mustask
WorkingDirectory=/opt/mustask/api
EnvironmentFile=/etc/mustask/mustask.env
Environment=HOME=/var/lib/mustask
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
ExecStart=/usr/bin/dotnet /opt/mustask/api/Mustask.Api.dll
Restart=on-failure
RestartSec=5
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/mustask
LimitNOFILE=65536

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now mustask-api
journalctl -u mustask-api -f       # структурированный JSON-лог в консоли
```

Если `dotnet` установлен в другом месте, поправьте `ExecStart`.
`ReadWritePaths` оставляет хранилище ключей Data Protection доступным на запись
при `ProtectSystem=strict`.

## 7. Reverse proxy и TLS (nginx)

`/etc/nginx/sites-available/mustask`:

```nginx
server {
    listen 80;
    server_name mustask.example.com;
    return 301 https://$host$request_uri;
}

server {
    # nginx 1.25.1 и новее: вместо этого `listen 443 ssl;` плюс `http2 on;`.
    listen 443 ssl http2;
    server_name mustask.example.com;

    ssl_certificate     /etc/letsencrypt/live/mustask.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/mustask.example.com/privkey.pem;

    # API: наружу открыт только /api/. /health и /alive остаются внутренними.
    location /api/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host  $host;
        proxy_read_timeout 60s;
        # Callback входа отвечает cookie сессии (зашифрованный тикет с
        # токенами, ~3.5 КБ на пустом realm и больше с каждым claim общего
        # realm) плюс очищаемыми OIDC-cookie. Стандартного буфера заголовков
        # 4–8 КБ мало: nginx отвечает 502.
        proxy_buffer_size 32k;
        proxy_buffers 8 32k;
        proxy_busy_buffers_size 64k;
    }

    # Панель: SPA, неизвестные пути отдают index.html.
    root /var/www/mustask;
    location / {
        try_files $uri /index.html;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/mustask /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

`ssl http2` в строке `listen` — для nginx, который ставят Ubuntu 24.04 и
Debian 12 (1.24 и 1.22): на `http2 on;` они останавливаются с `unknown
directive "http2"`. На 1.25.1 и новее форма в `listen` тоже работает и только
пишет предупреждение об устаревании.

HTTPS обязателен: cookie сессии `mustask.session` — `Secure`, `HttpOnly`,
`SameSite=Lax`, путь `/api/session`, и по обычному HTTP не отправляется
никогда.

## 8. Smoke-проверка

1. На VM: `curl -fsS http://127.0.0.1:5000/health` отвечает healthy. Проверка
   охватывает базу и доступность realm, поэтому сбой здесь — проблема
   подключения к базе или к Keycloak (в логе видно, к чему).
2. `https://mustask.example.com/` показывает панель; вход уводит на
   `keycloak.example.com` и после логина возвращает в панель.
3. Пользователь с `user` входит и попадает в панель. Пользователь **без**
   `user` возвращается с `authError=no_access`, и сессии у него нет.
4. Роли работают: пользователь только с `user` открывает справочники, но
   создать организацию ему отказано («Недостаточно прав»); первый
   администратор (все четыре роли, раздел 1) создаёт первую организацию. Если
   отказано и администратору, выполните для него проверку Evaluate из
   раздела 1: в `resource_access.mustask-api.roles` должны быть все четыре
   роли.
5. После первого входа пользователь находится в поиске участников (справочник
   заполняется при входе, импорта нет): создатель организации находит другого
   тестового пользователя по имени или e-mail при добавлении участника.

## 9. Обновление

1. Соберите новый релиз, как в разделе 4.
2. Примените новые миграции (раздел 3).
3. Замените `/var/www/mustask/` и `/opt/mustask/api/`, затем
   `sudo systemctl restart mustask-api`.

Откат кода — вернуть оба каталога; миграции автоматически не откатываются,
поэтому релизу с разрушающей миграцией нужна своя инструкция по откату.

## 10. Диагностика

| Симптом | Вероятная причина |
|---|---|
| API падает на старте, называя ключ `Keycloak:*` или `Session:PanelUrl` | В `mustask.env` нет переменной. |
| API падает на старте из-за секрета «known placeholder or shorter than 32» | `Keycloak__ClientSecret` — не настоящий секрет `mustask-panel`. |
| `nginx -t`: `unknown directive "http2"` | nginx старше 1.25.1: используйте `listen 443 ssl http2;` (раздел 7). |
| После логина в Keycloak браузер получает **502** на `/api/session/callback`; в error-логе nginx «upstream sent too big header» | Нет буферов прокси из раздела 7: cookie сессии не помещается в стандартный буфер заголовков nginx. |
| `/health` нездоров и называет realm; в логе ошибка TLS / сертификата | VM не доверяет CA сертификата Keycloak (раздел 2). |
| `dotnet ef` падает с `NETSDK1004` | Сначала выполните `dotnet restore src/backend/Mustask.Infrastructure` (раздел 3). |
| Keycloak показывает «Invalid redirect URI» | У `mustask-panel` нет `https://mustask.example.com/api/session/callback`, либо не задан `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, и API отправил адрес `http://`. |
| После входа каждый вызов API — 401 | В access token нет `aud = mustask-api`, или `Keycloak__Authority`/`Keycloak__Issuer` не совпадают с `iss` токена. |
| Вход возвращается с `authError=no_access` | У пользователя нет роли клиента `mustask-api` `user` (назначьте напрямую). |
| Вход есть, но роль не действует | В access token нет `resource_access.mustask-api.roles` (маппер, раздел 1). |
| После перезапуска всех разлогинило | `/var/lib/mustask` (ключи Data Protection) не сохраняется или недоступен службе на запись. |

## Что этот runbook не проверяет

Разделы 1–8 пройдены буквально 2026-11-05 (tech-056) на чистой Ubuntu 24.04 с
systemd вместо VM, с развёртыванием `main` на `927cf1d7`, против отдельного
Keycloak 26.4, realm которого настроен вручную ровно так, как просит раздел 1,
и где от MUSTASK только два клиента и четыре роли клиента. Раздел 8 там прошёл,
включая проверку Evaluate из раздела 1; найденные расхождения исправлены выше
(запись — в `docs/features/tech-056-vm-runbook-rehearsal/status.md`).

Не проверено до сих пор, потому что на репетиции этого не было:

- **Сеть и realm компании.** Настоящие DNS-имена, цепочка сертификатов
  компании, межсетевые экраны между VM, PostgreSQL и Keycloak, и realm, где уже
  есть собственные scopes, мапперы, политики, identity providers и required
  actions компании. Любое из этого может изменить токен (и его размер — см.
  буферы прокси в разделе 7) или процесс входа.
- **Вход через браузер.** Сертификаты репетиции были самоподписанными, поэтому
  вход проходил через настоящую форму логина, callback и cookie сессии
  скриптом, а не браузером; панель проверена на загрузку, но не прокликана.
- **SSH-туннель для миграций** (раздел 3): на репетиции база была доступна
  через временный проброс порта.
- **Bundle миграций, обновление и откат** (разделы 3 и 9): не выполнялись.
- **Адрес под под-путём**: этим релизом не поддерживается (см. начало
  документа, tech-057).

Первое настоящее развёртывание должно снова пройти раздел 8 и исправить этот
документ там, где он расходится с реальностью.
