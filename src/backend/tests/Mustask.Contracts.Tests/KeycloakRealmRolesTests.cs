using System.Text.Json;
using Mustask.Domain.Identity;

namespace Mustask.Contracts.Tests;

/// <summary>
/// Realm-import Keycloak — такой же артефакт контракта, как каталог ошибок (ADR-0018, ADR-0031). Модель доступа двухслойная:
/// <list type="bullet">
/// <item><b>роль</b> — клиентская роль клиента-ресурса <c>mustask-api</c>; их ровно четыре (<see cref="InstanceRoles"/>), имена и
/// описания совпадают с таблицей в коде;</item>
/// <item><b>право</b> — код из <see cref="PermissionCatalog"/>; в Keycloak его нет вовсе: таблица «роль → коды» живёт в коде, а
/// в токене едут только имена ролей (<c>resource_access.mustask-api.roles</c>).</item>
/// </list>
/// Общий realm компании разрешает нам только клиентов и клиентские роли, поэтому импорт не объявляет ни ролей realm'а, ни групп,
/// ни роли по умолчанию, ни композитов. Опечатка в имени роли не сломает ничего видимого — пользователь получит вечный 403 без
/// единой строки в логах: отсюда эти тесты.
/// </summary>
public class KeycloakRealmRolesTests
{
    private const string MustaskRealm = "mustask-realm.json";

    // --- Роли ↔ InstanceRoles -------------------------------------------------

    [Fact]
    public void MustaskRealm_DeclaresExactlyTheFourClientRolesOfTheTable_WithTheirDescriptions()
    {
        var declared = ClientRoles(MustaskRealm, "mustask-api")
            .ToDictionary(role => role.GetProperty("name").GetString()!, role => role.GetProperty("description").GetString(), StringComparer.Ordinal);

        Assert.Equal(InstanceRoles.Names.Order(StringComparer.Ordinal), declared.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["user", "organization-creator", "dictionaries-admin", "system-admin"], InstanceRoles.Names);

        foreach (var role in InstanceRoles.All)
        {
            Assert.Equal(role.Description, declared[role.Name]);
        }
    }

    [Fact]
    public void MustaskRealm_ClientRoleNames_AreNotPermissionCodes()
    {
        var leaked = ClientRoleNames(MustaskRealm, "mustask-api").Where(PermissionCatalog.AllCodes.Contains).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            leaked.Count == 0,
            $"Клиент `mustask-api` объявляет роли с именами кодов прав: {string.Join(", ", leaked)}. Токен несёт имена ролей, "
            + "коды выводит из них код (InstanceRoles); сырой код в токене прав не даёт.");
    }

    [Fact]
    public void MustaskRealm_ClientRoles_HaveNoComposites()
    {
        foreach (var role in ClientRoles(MustaskRealm, "mustask-api"))
        {
            Assert.False(role.TryGetProperty("composite", out var composite) && composite.GetBoolean(), $"Роль {role.GetProperty("name")} композитная.");
            Assert.False(role.TryGetProperty("composites", out _), $"Роль {role.GetProperty("name")} несёт композиты.");
        }
    }

    // --- tech-024 (ADR-0031): the company realm takes only clients and client roles ------------

    /// <summary>
    /// Общий realm компании: нам разрешены только клиенты и клиентские роли. Импорт стенда поэтому не объявляет ни ролей
    /// realm'а (встроенные <c>offline_access</c>, <c>uma_authorization</c> и роль по умолчанию Keycloak создаёт сам), ни групп,
    /// ни <c>defaultRole</c>, ни маппера <c>groups</c> — прочитать его было бы некому.
    /// </summary>
    [Fact]
    public void MustaskRealm_DeclaresNoRealmRolesGroupsOrDefaultRole()
    {
        var realm = Realm(MustaskRealm);

        Assert.True(!realm.GetProperty("roles").TryGetProperty("realm", out _), "Импорт объявляет роли realm'а.");
        Assert.False(realm.TryGetProperty("groups", out _), "Импорт объявляет группы.");
        Assert.False(realm.TryGetProperty("defaultRole", out _), "Импорт объявляет роль по умолчанию.");
        Assert.Equal(["mustask-api"], realm.GetProperty("roles").GetProperty("client").EnumerateObject().Select(client => client.Name));

        var panel = Client(MustaskRealm, "mustask-panel");
        var mappers = panel.GetProperty("protocolMappers").EnumerateArray().Select(mapper => mapper.GetProperty("name").GetString()).ToList();

        Assert.Equal(["audience-mustask-api"], mappers);
    }

    /// <summary>The realm name is configuration (<c>Keycloak:Realm</c>): the import reads it from MUSTASK_REALM, and no literal derived from it is left.</summary>
    [Fact]
    public void MustaskRealm_NameIsAPlaceholderWithTheStandDefault_AndNoNameDerivedLiteralIsLeft()
    {
        Assert.Equal("${MUSTASK_REALM:mustask}", Realm(MustaskRealm).GetProperty("realm").GetString());
        Assert.DoesNotContain("default-roles-", File.ReadAllText(RealmPath(MustaskRealm)), StringComparison.Ordinal);
    }

    /// <summary>The roles client scope keeps giving <c>resource_access</c>, and the audience mapper keeps <c>aud</c>.</summary>
    [Fact]
    public void MustaskPanel_KeepsTheRolesScopeAndTheAudienceMapper()
    {
        var panel = Client(MustaskRealm, "mustask-panel");

        Assert.Contains("roles", Names(panel.GetProperty("defaultClientScopes")));

        var audience = panel.GetProperty("protocolMappers").EnumerateArray().Single(mapper => mapper.GetProperty("name").GetString() == "audience-mustask-api");

        Assert.Equal("mustask-api", audience.GetProperty("config").GetProperty("included.client.audience").GetString());
        Assert.Equal("true", audience.GetProperty("config").GetProperty("access.token.claim").GetString());
    }

    // --- Клиенты ------------------------------------------------------------------

    /// <summary>
    /// Клиент панели обязан объявлять оба набора scope'ов, а не один.
    /// Это не вкусовщина, а пойманная руками ловушка импорта: указав у клиента
    /// только <c>optionalClientScopes</c>, Keycloak обнуляет и
    /// <c>defaultClientScopes</c> — клиент остаётся вообще без scope'ов, теряя
    /// вместе с ними `web-origins` (то есть CORS у SPA), `roles` (то есть
    /// `resource_access` с правами) и базовые claim'ы. Ломается это молча и
    /// проявляется уже в браузере.
    ///
    /// Заодно проверяем, что `offline_access` нет ни в одном из наборов:
    /// отключить его иначе нельзя — роль и её место в `default-roles-<realm>`
    /// Keycloak восстанавливает сам, поверх импорта.
    /// </summary>
    [Theory]
    [InlineData(MustaskRealm, "mustask-panel")]
    public void PanelClient_DeclaresBothScopeSets_AndNoOfflineAccess(string fileName, string clientId)
    {
        var client = Client(fileName, clientId);

        var required = new[] { "basic", "acr", "profile", "email", "roles", "web-origins" };

        Assert.True(
            client.TryGetProperty("defaultClientScopes", out var defaults),
            $"У клиента `{clientId}` не задан `defaultClientScopes`. Оба набора задаются только "
            + "вместе: указав один, импорт Keycloak обнуляет второй.");

        var defaultNames = Names(defaults);

        var lost = required.Where(scope => !defaultNames.Contains(scope)).ToList();

        Assert.True(
            lost.Count == 0,
            $"У клиента `{clientId}` в `defaultClientScopes` нет: {string.Join(", ", lost)}. "
            + "Пустой или урезанный набор — это не «меньше лишнего», а сломанный вход: без "
            + "`web-origins` браузер упрётся в CORS, без `roles` в токене не будет "
            + "`resource_access` с правами. Ломается молча, видно только в браузере.");

        Assert.True(
            client.TryGetProperty("optionalClientScopes", out var optional),
            $"У клиента `{clientId}` не задан `optionalClientScopes`. Без него клиент получает "
            + "набор по умолчанию, где есть `offline_access`.");

        foreach (var (property, names) in new[]
        {
            ("defaultClientScopes", defaultNames),
            ("optionalClientScopes", Names(optional)),
        })
        {
            Assert.False(
                names.Contains("offline_access"),
                $"У клиента `{clientId}` в `{property}` есть `offline_access`. Такой токен переживает "
                + "выход пользователя и обходит ssoSessionMaxLifespan; пустой optionalClientScopes — "
                + "единственный работающий способ его запретить.");
        }
    }

    /// <summary>
    /// ADR-0022 Decision 4: <c>mustask-panel</c> — confidential-клиент (браузер
    /// не ходит на Keycloak напрямую, обмен кода и продление делает
    /// <c>Mustask.Api</c>): общий набор флагов безопасности клиента плюс
    /// утверждения про <c>publicClient</c>/<c>secret</c>. Реального значения
    /// секрета файл не несёт (плейсхолдер, подставляется
    /// <c>MUSTASK_PANEL_CLIENT_SECRET</c> на стенде) — тест проверяет только факт
    /// присутствия поля, не его содержимое.
    /// </summary>
    [Fact]
    public void ConfidentialClient_MustaskPanel_KeepsExpectedSecurityFlags()
    {
        var client = Client(MustaskRealm, "mustask-panel");

        Assert.False(
            client.GetProperty("publicClient").GetBoolean(),
            "`mustask-panel` обязан быть confidential-клиентом (ADR-0022 Decision 4): секрет клиента — "
            + "условие, при котором утёкший refresh-токен нельзя обменять без него.");

        Assert.True(
            client.TryGetProperty("secret", out var secret) && !string.IsNullOrEmpty(secret.GetString()),
            "У `mustask-panel` нет поля `secret` — без него импорт создаст клиента без секрета "
            + "(Keycloak сгенерирует случайный, который не совпадёт с Keycloak:ClientSecret бэкенда).");

        AssertSharedClientSecurityFlags(client, "mustask-panel");
    }

    /// <summary>
    /// A default value here (e.g. `${MUSTASK_PANEL_CLIENT_SECRET:dev-only-placeholder-secret}`)
    /// is exactly the wrong place for one — the stand doesn't need it (`Mustask.AppHost` always sets the
    /// variable), and it fires precisely where it must not: importing this
    /// same file without AppHost and without the variable set. The bare
    /// placeholder is not a complete fix by itself (an unresolved
    /// `${MUSTASK_PANEL_CLIENT_SECRET}` is itself a known literal — see
    /// <c>SessionOidcOptions.FromConfiguration</c>'s own check), but it is
    /// the half of the fix a file test can hold the line on.
    /// </summary>
    [Fact]
    public void MustaskPanelClientSecret_HasNoDefaultValue()
    {
        var secret = Client(MustaskRealm, "mustask-panel").GetProperty("secret").GetString();

        Assert.Equal("${MUSTASK_PANEL_CLIENT_SECRET}", secret);
    }

    // --- ADR-0023: the production import stays a closed set ---------------------

    /// <summary>
    /// `AppHost.cs` bind-mounts the whole `deploy/keycloak/realms` directory
    /// and `--import-realm` imports every file it finds there. A second file
    /// — declaring a client with `serviceAccountsEnabled: true` — would be
    /// imported by every stand and seen by no fact that iterates a hardcoded
    /// file name, because nothing enumerated the directory. This test makes the file set
    /// itself a closed, named allowlist: adding (or removing, or renaming)
    /// a realm-import file is a decision that fails here until a human
    /// updates the list, the same review attention a new client already
    /// gets from <see cref="ProductionRealmImport_ClientAllowlistIsClosed"/>.
    /// </summary>
    [Fact]
    public void KeycloakRealmsDirectory_ContainsExactlyTheAllowlistedFiles()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal) { MustaskRealm };
        var actual = RealmDirectoryFileNames().ToHashSet(StringComparer.Ordinal);

        var unexpected = actual.Except(expected).ToList();
        var missing = expected.Except(actual).ToList();

        Assert.True(
            unexpected.Count == 0 && missing.Count == 0,
            "Набор *.json файлов в deploy/keycloak/realms/ разошёлся с ожидаемым "
            + $"Лишние: {string.Join(", ", unexpected)}. "
            + $"Отсутствуют: {string.Join(", ", missing)}. `--import-realm` (AppHost.cs) "
            + "импортирует всю директорию целиком, а не названные файлы — новый файл "
            + "здесь попадёт в каждый стенд молча, пока кто-то не обновит и этот список, "
            + "и обе проверки клиентов ниже.");
    }

    /// <summary>
    /// The production realm-import is exactly the
    /// files `--import-realm` mounts (`AppHost.cs`) — the set
    /// <see cref="KeycloakRealmsDirectory_ContainsExactlyTheAllowlistedFiles"/>
    /// guards — and the set of `clientId`s across all of them is closed and
    /// named here. Adding any client — the stand-only `mustask-e2e` included,
    /// should it ever be added here by mistake — fails this test until a
    /// human edits the allowlist alongside the realm-import change, the same
    /// review attention a new client should get.
    /// </summary>
    [Fact]
    public void ProductionRealmImport_ClientAllowlistIsClosed()
    {
        var allowlist = new HashSet<string>(StringComparer.Ordinal)
        {
            "mustask-panel", "mustask-api",
        };

        var actual = RealmDirectoryFileNames().SelectMany(ClientIds).ToHashSet(StringComparer.Ordinal);

        var unexpected = actual.Except(allowlist).ToList();
        var missing = allowlist.Except(actual).ToList();

        Assert.True(
            unexpected.Count == 0 && missing.Count == 0,
            "Набор clientId в production realm-import разошёлся с закрытым списком "
            + $"Лишние: {string.Join(", ", unexpected)}. "
            + $"Отсутствуют: {string.Join(", ", missing)}. Новый клиент — это решение, "
            + "которое требует внимания ревью, а не побочный эффект правки JSON: "
            + "обнови allowlist в этом тесте осознанно, если добавление клиента "
            + "в production-импорт действительно то, что нужно.");
    }

    /// <summary>
    /// The property that actually matters
    /// — obtaining the impersonation right requires a service account, and a
    /// service account requires `serviceAccountsEnabled: true`. This needs no
    /// Keycloak role vocabulary (the production import files do not declare
    /// the `realm-management` client at all) and breaks the moment any
    /// listed client turns a service account on, impersonation-capable or
    /// not, whatever a future right is named. Iterates
    /// <see cref="RealmDirectoryFileNames"/>, not a hardcoded file name, so a
    /// second realm-import file is scanned too, not only allowlisted by name.
    /// </summary>
    [Fact]
    public void ProductionRealmImport_NoClientHasServiceAccountEnabled()
    {
        var offenders = new List<string>();

        foreach (var fileName in RealmDirectoryFileNames())
        {
            foreach (var clientId in ClientIds(fileName))
            {
                var client = Client(fileName, clientId);

                if (client.TryGetProperty("serviceAccountsEnabled", out var flag) && flag.GetBoolean())
                {
                    offenders.Add($"{clientId} ({fileName})");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"В production realm-import у клиента(ов) включён serviceAccountsEnabled: {string.Join(", ", offenders)}. "
            + "Право на имперсонацию требует сервис-аккаунта (ADR-0023) — сервис-аккаунт "
            + "в production-импорте это ровно та форма, которую ADR-0023 запрещает, независимо от "
            + "того, как названы или переименованы роли клиента.");
    }

    /// <summary>
    /// Флаги, на которых держится безопасность клиента панели, — всё, что не
    /// зависит от <c>publicClient</c>/<c>secret</c> самих по себе. Каждый из
    /// них выключается одним словом в JSON, и ни один не выдаёт себя при
    /// беглом чтении диффа: клиент продолжает работать, просто хуже.
    /// </summary>
    private static void AssertSharedClientSecurityFlags(JsonElement client, string clientId)
    {
        Assert.Equal(
            "S256",
            client.GetProperty("attributes").GetProperty("pkce.code.challenge.method").GetString());

        foreach (var flag in new[] { "directAccessGrantsEnabled", "implicitFlowEnabled", "serviceAccountsEnabled" })
        {
            Assert.False(
                client.GetProperty(flag).GetBoolean(),
                $"У `{clientId}` включён `{flag}`. Это обход PKCE: "
                + "password grant и implicit flow отдают токен без обмена кодом. "
                + "Включать ради удобства скрипта нельзя — скрипту нужен свой клиент.");
        }

        var mappers = client.GetProperty("protocolMappers").EnumerateArray().ToList();

        Assert.True(
            mappers.Count(mapper => mapper.GetProperty("protocolMapper").GetString() == "oidc-audience-mapper") == 1,
            $"У `{clientId}` должен быть ровно один audience-маппер: без него в токене нет `aud` "
            + "ресурсного сервера, и тот обязан будет принимать любой токен своего realm'а.");

        // Права читаются из штатного `resource_access`, который даёт scope
        // `roles`. Свой маппер ролей realm'а — это тот самый claim с
        // `offline_access` и `default-roles-*` внутри, от которого ушли.
        var roleMappers = mappers
            .Where(mapper => mapper.GetProperty("protocolMapper").GetString()
                is "oidc-usermodel-realm-role-mapper" or "oidc-usermodel-client-role-mapper")
            .Select(mapper => mapper.GetProperty("name").GetString())
            .ToList();

        Assert.True(
            roleMappers.Count == 0,
            $"У `{clientId}` есть свой маппер ролей: {string.Join(", ", roleMappers)}. Права "
            + "приходят в `resource_access.<клиент>.roles` через scope `roles`; отдельный claim "
            + "дублирует его и тащит служебные роли Keycloak.");
    }

    // --- Realm: сроки, политика, тема -------------------------------------------

    /// <summary>
    /// Сроки жизни и защита от перебора. Если их поменяют, пусть это будет
    /// решением, а не побочным эффектом правки соседнего поля.
    /// </summary>
    [Theory]
    [InlineData(MustaskRealm)]
    public void Realm_KeepsItsSessionAndPasswordPolicy(string fileName)
    {
        var realm = Realm(fileName);

        Assert.Equal(900, realm.GetProperty("accessTokenLifespan").GetInt32());
        Assert.Equal(43200, realm.GetProperty("ssoSessionMaxLifespan").GetInt32());
        Assert.Equal(43200, realm.GetProperty("offlineSessionMaxLifespan").GetInt32());
        Assert.True(realm.GetProperty("offlineSessionMaxLifespanEnabled").GetBoolean());

        Assert.True(
            realm.GetProperty("bruteForceProtected").GetBoolean(),
            $"В realm'е из `{fileName}` выключена защита от перебора пароля.");

        Assert.True(
            realm.GetProperty("revokeRefreshToken").GetBoolean(),
            $"В realm'е из `{fileName}` выключена ротация refresh-токенов. Без неё refresh, "
            + "утёкший из браузера, живёт до конца SSO-сессии и переиспользуется незаметно: "
            + "повторное предъявление — единственный признак кражи, и он теряется.");

        Assert.Equal(0, realm.GetProperty("refreshTokenMaxReuse").GetInt32());

        Assert.False(
            realm.GetProperty("registrationAllowed").GetBoolean(),
            $"В realm'е из `{fileName}` открыта самостоятельная регистрация. Учётные записи "
            + "в системе заводит администратор.");
    }

    /// <summary>
    /// Тема входа, названная в realm'е, обязана существовать, а файлы из её
    /// `styles` — лежать на диске. Обе поломки тихие: Keycloak на неизвестную
    /// тему молча берёт штатную, а на пропавший css отдаёт страницу без
    /// стилей. Ни то, ни другое не роняет старт и не пишет в лог ничего, что
    /// попалось бы на глаза.
    /// </summary>
    [Theory]
    [InlineData(MustaskRealm)]
    public void Realm_LoginTheme_ExistsWithAllItsStylesheets(string fileName)
    {
        var theme = Realm(fileName).GetProperty("loginTheme").GetString()!;
        var themeDirectory = Path.Combine(
            RepositoryLayout.Root.FullName, "deploy", "keycloak", "themes", theme, "login");

        Assert.True(
            Directory.Exists(themeDirectory),
            $"Realm из `{fileName}` ссылается на тему `{theme}`, а каталога {themeDirectory} нет. "
            + "Keycloak в этом случае молча покажет штатную тему.");

        var properties = Path.Combine(themeDirectory, "theme.properties");

        Assert.True(File.Exists(properties), $"У темы `{theme}` нет theme.properties.");

        var styles = File.ReadAllLines(properties)
            .FirstOrDefault(line => line.StartsWith("styles=", StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"В theme.properties темы `{theme}` нет ключа `styles`. Он перекрывает "
                + "родительский целиком, поэтому в нём обязан быть и css родителя.");

        foreach (var file in styles["styles=".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // css родительской темы (`css/styles.css`) приезжает из образа
            // Keycloak, у нас на диске его нет и быть не должно.
            if (file is "css/styles.css")
            {
                continue;
            }

            var path = Path.Combine(themeDirectory, "resources", file.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(
                File.Exists(path),
                $"Тема `{theme}` объявляет в `styles` файл `{file}`, которого нет: {path}. "
                + "Страница входа отрисуется без него и без единой ошибки.");
        }
    }

    // --- Разбор realm-import ------------------------------------------------------

    // Документы не освобождаются намеренно: JsonElement живёт, пока жив
    // документ, а элементы здесь возвращаются наружу. Файлы маленькие, тесты
    // короткие — освобождение оставлено сборщику.

    private static JsonElement Realm(string fileName) =>
        JsonDocument.Parse(File.ReadAllText(RealmPath(fileName))).RootElement;

    private static IEnumerable<JsonElement> ClientRoles(string fileName, string clientId) =>
        Realm(fileName).GetProperty("roles").GetProperty("client").GetProperty(clientId).EnumerateArray();

    private static HashSet<string> ClientRoleNames(string fileName, string clientId)
    {
        var clients = Realm(fileName).GetProperty("roles").GetProperty("client");

        Assert.True(
            clients.TryGetProperty(clientId, out var roles),
            $"В `{fileName}` у клиента `{clientId}` не объявлено ни одной роли — а это его права.");

        return roles.EnumerateArray()
            .Select(role => role.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// All `clientId`s declared in one realm-import file.
    /// `clients` is optional in Keycloak's realm representation — a realm that
    /// declares only roles, groups or an identity provider is legal and has no
    /// such array. Read it with TryGetProperty: the two client-scanning facts
    /// must report "this file declares no client", not throw a bare
    /// KeyNotFoundException naming neither the file nor the property.
    /// </summary>
    private static IEnumerable<string> ClientIds(string fileName) =>
        Realm(fileName).TryGetProperty("clients", out var clients)
            ? clients.EnumerateArray().Select(client => client.GetProperty("clientId").GetString()!)
            : [];

    private static JsonElement Client(string fileName, string clientId)
    {
        var client = Realm(fileName)
            .GetProperty("clients")
            .EnumerateArray()
            .FirstOrDefault(candidate => candidate.GetProperty("clientId").GetString() == clientId);

        Assert.True(
            client.ValueKind == JsonValueKind.Object,
            $"В realm-import `{fileName}` нет клиента `{clientId}`.");

        return client;
    }

    private static string RealmPath(string fileName)
    {
        var path = Path.Combine(RepositoryLayout.Root.FullName, "deploy", "keycloak", "realms", fileName);

        Assert.True(File.Exists(path), $"Не найден realm-import: {path}");

        return path;
    }

    /// <summary>
    /// The directory `AppHost.cs` bind-mounts whole (`--import-realm` reads
    /// every `*.json` file it finds there), enumerated rather than assumed to
    /// hold exactly <see cref="MustaskRealm"/> — that assumption
    /// is what <see cref="KeycloakRealmsDirectory_ContainsExactlyTheAllowlistedFiles"/>
    /// checks explicitly, instead of leaving it implicit in every other
    /// test's `[InlineData]`.
    /// </summary>
    private static List<string> RealmDirectoryFileNames() =>
        Directory.EnumerateFiles(
                Path.Combine(RepositoryLayout.Root.FullName, "deploy", "keycloak", "realms"), "*.json")
            .Select(Path.GetFileName)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    private static List<string?> Names(JsonElement array) =>
        array.EnumerateArray().Select(element => element.GetString()).ToList();
}
