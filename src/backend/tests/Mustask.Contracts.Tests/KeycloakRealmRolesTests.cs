using System.Text.Json;
using Mustask.AppHost.Provisioning;
using Mustask.Domain.Identity;

namespace Mustask.Contracts.Tests;

/// <summary>
/// Realm-import Keycloak — такой же артефакт контракта, как каталог ошибок
/// (ADR-0018). Модель доступа трёхслойная, как в Azure RBAC:
/// <list type="bullet">
/// <item><b>право</b> — роль клиента-ресурса (`mustask-api`);
/// имя роли и код права совпадают посимвольно, это то, что проверяет код;</item>
/// <item><b>роль</b> — композитная роль realm'а, именованный набор прав;
/// редактирует администратор;</item>
/// <item><b>группа</b> — оргструктура, на неё мапятся роли; код имён групп
/// не видит.</item>
/// </list>
/// В токене всё это схлопывается в <c>resource_access.&lt;клиент&gt;.roles</c> —
/// плоский список кодов прав, без служебных ролей Keycloak.
///
/// Почему это тесты, а не «внимательность при правке»: после переноса identity
/// в Keycloak у <see cref="PermissionCatalog"/> не остаётся ни одного
/// потребителя-валидатора. Опечатка в имени права, в композите или в маппинге
/// группы не сломает ничего видимого — политика просто никогда не найдёт
/// нужный claim, и пользователь получит вечный 403 без единой строки в логах.
/// </summary>
public class KeycloakRealmRolesTests
{
    private const string MustaskRealm = "mustask-realm.json";

    /// <summary>The instance administrator (mod-016 BR-M1-29): dictionaries, and the right to create organizations. Nothing inside an organization.</summary>
    private const string InstanceAdminRole = "instance-admin";

    /// <summary>The realm's default roles (Keycloak names it <c>default-roles-&lt;realm&gt;</c>): what every user holds, a user in no group included.</summary>
    private const string DefaultRole = "default-roles-mustask";

    // --- Права ↔ PermissionCatalog --------------------------------------------

    [Fact]
    public void MustaskRealm_DeclaresClientRoleForEveryPermissionCode()
    {
        var permissions = ClientRoleNames(MustaskRealm, "mustask-api");

        var missing = PermissionCatalog.InstanceCodes
            .Where(code => !permissions.Contains(code))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"В realm-import `mustask` у клиента `mustask-api` нет ролей для кодов прав: {string.Join(", ", missing)}. "
            + "Каждый код из PermissionCatalog обязан существовать ролью клиента — "
            + "иначе право нельзя включить в роль, и проверка молча даёт 403.");
    }

    [Fact]
    public void MustaskRealm_HasNoClientRolesOutsideThePermissionCatalog()
    {
        var extra = ClientRoleNames(MustaskRealm, "mustask-api")
            .Where(role => !PermissionCatalog.InstanceCodes.Contains(role))
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            extra.Count == 0,
            $"У клиента `mustask-api` есть роли, которых нет в PermissionCatalog: {string.Join(", ", extra)}. "
            + "Реестр прав закрыт: право, которое не проверяет ни одна политика, "
            + "создаёт ложное впечатление выданного доступа.");
    }

    /// <summary>
    /// Защита от отката к плоской модели, в которой права были ролями realm'а
    /// и понятия «роль как набор прав» не существовало.
    /// </summary>
    [Fact]
    public void MustaskRealm_RealmRolesAreBusinessRoles_NotPermissionCodes()
    {
        var leaked = RealmRoleNames(MustaskRealm)
            .Where(PermissionCatalog.AllCodes.Contains)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            leaked.Count == 0,
            $"В realm `mustask` есть роли realm'а с именами кодов прав: {string.Join(", ", leaked)}. "
            + "Права живут ролями клиента `mustask-api`, роли realm'а — это именованные наборы "
            + "прав (admin, member, ...). Право ролью realm'а попадёт в realm_access "
            + "вместо resource_access, и Mustask.Api его не увидит.");
    }

    // --- Роли: композиты ссылаются только на объявленные права ----------------

    /// <summary>
    /// Каждая роль realm'а — композит из прав клиента-ресурса, и только из
    /// них. Право с опечаткой в композите Keycloak при импорте не создаст и
    /// не отвергнет — оно просто не выдастся, а роль будет выглядеть полной.
    /// Композиты из композитов запрещены — документация Keycloak просит
    /// composite roles «not overused», и одного уровня нам хватает.
    /// </summary>
    [Theory]
    [InlineData(MustaskRealm, "mustask-api")]
    public void Realm_EveryRoleIsCompositeOfDeclaredPermissionsOnly(string fileName, string resourceClient)
    {
        var permissions = ClientRoleNames(fileName, resourceClient);

        // mod-016 (BR-M1-37): the realm's default role and the two realm roles Keycloak itself puts in it are declared in the
        // import, so the drift check and the tests read one declaration — they are not business roles and have their own tests
        // (DefaultRole_HoldsReadAndOnlyKnownBuiltIns). Nothing else is exempt: no loosening for the rest.
        var roles = BusinessRoles(fileName).ToList();

        Assert.NotEmpty(roles);

        foreach (var role in roles)
        {
            var name = role.GetProperty("name").GetString();

            Assert.True(
                role.TryGetProperty("composite", out var composite) && composite.GetBoolean(),
                $"Роль `{name}` в `{fileName}` не композитная. Роль realm'а — это набор прав; "
                + "роль без прав ничего не даёт и только вводит в заблуждение.");

            var composites = role.GetProperty("composites");

            Assert.False(
                composites.TryGetProperty("realm", out _),
                $"Роль `{name}` в `{fileName}` включает другие роли realm'а. Композиты из "
                + "композитов не заводим: один уровень — роль из прав.");

            var granted = Names(composites.GetProperty("client").GetProperty(resourceClient));

            Assert.NotEmpty(granted);

            var unknown = granted.Where(permission => !permissions.Contains(permission!)).ToList();

            Assert.True(
                unknown.Count == 0,
                $"Роль `{name}` в `{fileName}` ссылается на права, которых у клиента "
                + $"`{resourceClient}` нет: {string.Join(", ", unknown)}. Keycloak при импорте "
                + "этого не проверяет — право молча не выдастся.");
        }
    }

    /// <summary>
    /// Хотя бы одна роль обязана держать все права экземпляра — и только их, — и хотя бы одна
    /// группа обязана её выдавать: иначе после импорта в системе нет никого, кто мог бы что-то
    /// исправить (doc-006 constraint 3). mod-016 (BR-M1-29): роль называется <c>instance-admin</c>;
    /// она держит ровно коды уровня экземпляра и ни одного кода организации — права внутри
    /// организации выводятся из членства, а не из роли Keycloak.
    /// </summary>
    [Theory]
    [InlineData(MustaskRealm, "mustask-api")]
    public void Realm_HasInstanceAdminWithEveryInstanceCodeAndNoOther_MappedToSomeGroup(string fileName, string resourceClient)
    {
        var permissions = ClientRoleNames(fileName, resourceClient);

        var instanceAdmin = RealmRoles(fileName)
            .SingleOrDefault(role => role.GetProperty("name").GetString() == InstanceAdminRole);

        Assert.True(
            instanceAdmin.ValueKind == JsonValueKind.Object,
            $"В `{fileName}` нет роли `{InstanceAdminRole}`.");

        var granted = Names(instanceAdmin.GetProperty("composites").GetProperty("client").GetProperty(resourceClient))
            .ToHashSet(StringComparer.Ordinal);

        var missing = permissions.Where(permission => !granted.Contains(permission)).ToList();

        Assert.True(
            missing.Count == 0,
            $"Роль `{InstanceAdminRole}` в `{fileName}` не держит права: {string.Join(", ", missing)}. "
            + "Право, которого нет у администратора экземпляра, нельзя выдать никому без правки импорта.");

        Assert.Equal(
            PermissionCatalog.InstanceCodes.Order(StringComparer.Ordinal),
            granted.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(granted, PermissionCatalog.OrganizationCodes.Contains);

        Assert.True(
            AllGroups(fileName).Any(group => GroupRoles(group).Contains(InstanceAdminRole)),
            $"В `{fileName}` ни одна группа не выдаёт роль `{InstanceAdminRole}`. После импорта в realm'е "
            + "не будет ни одного администратора экземпляра.");
    }

    // --- mod-016: the inventory of the realm (BR-M1-29, US-8) ---------------------

    /// <summary>
    /// US-8 · "The realm import holds exactly the named inventory": the realm roles are the instance administrator, the
    /// dictionaries administrator, the organization creator and the realm's default roles (the default role and the two realm roles
    /// Keycloak itself puts in it); the groups are exactly /Администраторы, /Администраторы справочников and /Создатели организаций.
    /// No role is named <c>admin</c>, <c>member</c> or <c>viewer</c> — a Keycloak role that pretends to grant organization rights —
    /// and there is no group /Сотрудники.
    /// </summary>
    [Fact]
    public void RealmInventory_IsExactlyTheNamedOne()
    {
        var roles = RealmRoleNames(MustaskRealm);
        var groupPaths = AllGroupPaths(MustaskRealm);

        Assert.Equal(
            new[] { InstanceAdminRole, "dictionaries-admin", "organization-creator", DefaultRole }
                .Concat(KeycloakStandDriftCheck.BuiltInRealmRoles)
                .Order(StringComparer.Ordinal),
            roles.Order(StringComparer.Ordinal));
        Assert.Equal(
            new[] { "/Администраторы", "/Администраторы справочников", "/Создатели организаций" }.Order(StringComparer.Ordinal),
            groupPaths.Order(StringComparer.Ordinal));
        Assert.Empty(roles.Intersect(new[] { "admin", "member", "viewer" }, StringComparer.Ordinal));
        Assert.DoesNotContain(groupPaths, path => path.StartsWith("/Сотрудники", StringComparison.Ordinal));
    }

    /// <summary>US-8 · each group maps exactly the role the inventory names for it.</summary>
    [Fact]
    public void Groups_MapExactlyTheirNamedRoles()
    {
        var mapping = AllGroups(MustaskRealm).ToDictionary(
            group => group.GetProperty("name").GetString()!,
            group => GroupRoles(group).Order(StringComparer.Ordinal).ToList(),
            StringComparer.Ordinal);

        Assert.Equal([InstanceAdminRole], mapping["Администраторы"]);
        Assert.Equal(["dictionaries-admin"], mapping["Администраторы справочников"]);
        Assert.Equal(["organization-creator"], mapping["Создатели организаций"]);
    }

    /// <summary>
    /// US-8 / BR-M1-29, BR-M1-37 · "Every signed-in user reads the dictionaries": the realm's default roles hold
    /// <c>dictionaries.languages.read</c> — declared in a form the contract tests and the stand's drift check both read (the role is
    /// in <c>roles.realm</c> with its composites, and the top-level <c>defaultRole</c> names it; spike of plan-backend § 4.3: Keycloak
    /// 26.4 imports exactly this) — and no other code of <c>mustask-api</c>. Its other composites are exactly the built-ins Keycloak
    /// gives every realm: <c>offline_access</c>, <c>uma_authorization</c> and the account client's <c>view-profile</c> and
    /// <c>manage-account</c>.
    /// </summary>
    [Fact]
    public void DefaultRole_HoldsReadAndOnlyKnownBuiltIns()
    {
        var realm = Realm(MustaskRealm);
        var role = RealmRoles(MustaskRealm).SingleOrDefault(candidate => candidate.GetProperty("name").GetString() == DefaultRole);

        Assert.True(role.ValueKind == JsonValueKind.Object, $"В `{MustaskRealm}` нет роли `{DefaultRole}` в roles.realm.");
        Assert.Equal(DefaultRole, realm.GetProperty("defaultRole").GetProperty("name").GetString());
        Assert.True(role.GetProperty("composite").GetBoolean());

        var composites = role.GetProperty("composites");
        var client = composites.GetProperty("client");

        Assert.Equal([PermissionCatalog.DictionariesLanguagesRead], Names(client.GetProperty("mustask-api")));
        Assert.Equal(
            ["manage-account", "view-profile"],
            Names(client.GetProperty("account")).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(["account", "mustask-api"], client.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            KeycloakStandDriftCheck.BuiltInRealmRoles.Order(StringComparer.Ordinal),
            Names(composites.GetProperty("realm")).OrderBy(name => name, StringComparer.Ordinal));

        // The two built-ins are declared without composites of their own: nothing hides in them.
        foreach (var builtIn in KeycloakStandDriftCheck.BuiltInRealmRoles)
        {
            var declared = RealmRoles(MustaskRealm).Single(candidate => candidate.GetProperty("name").GetString() == builtIn);

            Assert.False(declared.TryGetProperty("composites", out _), $"The built-in `{builtIn}` carries composites.");
        }
    }

    // --- Группы: ссылаются только на объявленные роли -------------------------

    [Theory]
    [InlineData(MustaskRealm)]
    public void Realm_GroupsMapOnlyDeclaredRoles(string fileName)
    {
        var roles = RealmRoleNames(fileName);
        var groups = AllGroups(fileName).ToList();

        Assert.NotEmpty(groups);

        foreach (var group in groups)
        {
            var name = group.GetProperty("name").GetString();
            var unknown = GroupRoles(group).Where(role => !roles.Contains(role)).ToList();

            Assert.True(
                unknown.Count == 0,
                $"Группа `{name}` в `{fileName}` выдаёт роли, которых нет в realm'е: "
                + $"{string.Join(", ", unknown)}. Импорт этого не проверяет — членство в группе "
                + "не даст ничего.");
        }
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

    private static IEnumerable<JsonElement> RealmRoles(string fileName) =>
        Realm(fileName).GetProperty("roles").GetProperty("realm").EnumerateArray();

    private static HashSet<string> RealmRoleNames(string fileName) =>
        RealmRoles(fileName)
            .Select(role => role.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>The realm roles that are named sets of rights: every role but the default role and Keycloak's two built-ins in it.</summary>
    private static IEnumerable<JsonElement> BusinessRoles(string fileName) =>
        RealmRoles(fileName).Where(role =>
            role.GetProperty("name").GetString() is { } name
            && name != DefaultRole
            && !KeycloakStandDriftCheck.BuiltInRealmRoles.Contains(name));

    private static List<string> AllGroupPaths(string fileName)
    {
        var paths = new List<string>();

        void Visit(IEnumerable<JsonElement> groups, string parent)
        {
            foreach (var group in groups)
            {
                var path = parent + "/" + group.GetProperty("name").GetString();
                paths.Add(path);

                if (group.TryGetProperty("subGroups", out var subGroups))
                {
                    Visit(subGroups.EnumerateArray(), path);
                }
            }
        }

        if (Realm(fileName).TryGetProperty("groups", out var top))
        {
            Visit(top.EnumerateArray(), string.Empty);
        }

        return paths;
    }

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

    private static IEnumerable<JsonElement> AllGroups(string fileName)
    {
        var realm = Realm(fileName);

        return realm.TryGetProperty("groups", out var groups)
            ? groups.EnumerateArray().SelectMany(Flatten)
            : [];

        static IEnumerable<JsonElement> Flatten(JsonElement group)
        {
            yield return group;

            if (group.TryGetProperty("subGroups", out var subGroups))
            {
                foreach (var child in subGroups.EnumerateArray().SelectMany(Flatten))
                {
                    yield return child;
                }
            }
        }
    }

    private static List<string> GroupRoles(JsonElement group) =>
        group.TryGetProperty("realmRoles", out var roles)
            ? roles.EnumerateArray().Select(role => role.GetString()!).ToList()
            : [];

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
