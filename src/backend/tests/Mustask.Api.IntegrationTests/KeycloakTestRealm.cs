using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Тестовый realm Keycloak для интеграционных тестов: один
/// RSA-2048 ключ на прогон сборки, discovery-документ и выпуск токенов,
/// подписанных этим ключом. Живого Keycloak в тестах нет вовсе — discovery и
/// JWKS подменяются <see cref="FakeRealmMetadata"/>, а не запросом по сети.
/// </summary>
public static class KeycloakTestRealm
{
    /// <summary><c>Keycloak:Authority</c>/ожидаемый <c>iss</c> тестового realm'а.</summary>
    public const string Issuer = "http://keycloak.invalid/realms/mustask";

    /// <summary><c>Keycloak:Audience</c>/ожидаемый <c>aud</c> — клиент <c>mustask-api</c>.</summary>
    public const string Audience = "mustask-api";

    /// <summary>Другой realm — для негативных сценариев («чужой issuer», «чужой клиент»).</summary>
    public const string ForeignIssuer = "http://keycloak.invalid/realms/other";

    /// <summary>Клиент другого приложения — для сценария «роли чужого клиента прав не дают».</summary>
    public const string ForeignClient = "other-api";

    private static readonly RSA Key = RSA.Create(2048);

    /// <summary><c>kid</c> тестового ключа — RFC 7638 не требуется, тесты не проверяют его форму, только совпадение.</summary>
    public static readonly string Kid = Guid.CreateVersion7().ToString("N");

    private static readonly RsaSecurityKey SecurityKey = new(Key) { KeyId = Kid };

    private static readonly JwtSecurityTokenHandler Handler = new();

    /// <summary>
    /// Discovery-документ «здорового» realm'а — то же, что вернул бы настоящий
    /// Keycloak: ожидаемый <c>issuer</c> и один ключ JWKS. Используется
    /// <see cref="FakeRealmMetadata"/> в режиме <see cref="FakeRealmMetadata.Mode.Ok"/>
    /// (умолчание, единственное состояние, безопасное для общей фабрики).
    /// </summary>
    public static OpenIdConnectConfiguration Configuration { get; } = BuildConfiguration(Issuer, includeKeys: true);

    /// <summary>Мутируемые параметры выдаваемого токена — по умолчанию действующий токен с полным набором заявленных прав.</summary>
    public sealed class TokenOptions
    {
        public string Issuer { get; set; } = KeycloakTestRealm.Issuer;

        public string Audience { get; set; } = KeycloakTestRealm.Audience;

        public string Kid { get; set; } = KeycloakTestRealm.Kid;

        public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset Expires { get; set; } = DateTimeOffset.UtcNow.AddMinutes(15);

        public string Subject { get; set; } = Guid.CreateVersion7().ToString();

        public string PreferredUsername { get; set; } = "test-user";

        /// <summary><c>null</c> — claim <c>name</c> отсутствует вовсе (для сценария «name → preferred_username»).</summary>
        public string? Name { get; set; } = "Test User";

        /// <summary><c>resource_access.&lt;Audience&gt;.roles</c> — права запроса. <c>null</c> — ключ <c>mustask-api</c> из <c>resource_access</c> не пишется вовсе.</summary>
        public IReadOnlyCollection<string>? Permissions { get; set; } = [];

        /// <summary><c>realm_access.roles</c> — для негативного сценария («роли realm'а прав не дают»), не читается <see cref="Mustask.Api.Authentication.KeycloakPermissionClaims"/>.</summary>
        public IReadOnlyCollection<string>? RealmAccessRoles { get; set; }

        /// <summary>Права другого клиента в <c>resource_access</c> (по умолчанию — <see cref="ForeignClient"/>) — тоже не должны учитываться.</summary>
        public IReadOnlyCollection<string>? ForeignClientPermissions { get; set; }

        public string ForeignClientId { get; set; } = ForeignClient;

        /// <summary>
        /// ADR-0022: claim <c>sid</c> — только для id-токенов, выданных
        /// <see cref="KeycloakTokenEndpointResponder"/> в сценариях модуля
        /// сессии. <c>null</c> (умолчание) — claim не пишется вовсе, чтобы не
        /// затрагивать access-токены остальных тестов.
        /// </summary>
        public string? Sid { get; set; }

        /// <summary>
        /// Claim <c>nonce</c> — OIDC-протокол требует его в
        /// id-токене, значение сверяется с nonce-cookie, которую сам же
        /// challenge и выставил. <c>null</c> (умолчание) — claim не пишется.
        /// </summary>
        public string? Nonce { get; set; }

        /// <summary>
        /// Claim <c>email</c> — client scope
        /// <c>email</c> в <c>deploy/keycloak/realms/mustask-realm.json</c>. Только
        /// для калибровки размера id-токена на реалистичном профиле;
        /// <c>SessionAuthenticationSetup</c> этот claim с тикета не
        /// читает. <c>null</c> (умолчание) — claim не пишется.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>Claim <c>email_verified</c>, тот же client scope <c>email</c>.</summary>
        public bool? EmailVerified { get; set; }

        /// <summary>Claim <c>given_name</c>, client scope <c>profile</c>.</summary>
        public string? GivenName { get; set; }

        /// <summary>Claim <c>family_name</c>, client scope <c>profile</c>.</summary>
        public string? FamilyName { get; set; }

        /// <summary>
        /// Claim <c>groups</c> — маппер
        /// <c>oidc-group-membership-mapper</c> (<c>full.path=true</c>) клиента
        /// <c>mustask-panel</c> в <c>deploy/keycloak/realms/mustask-realm.json</c>.
        /// <c>null</c> (умолчание) — claim не пишется.
        /// </summary>
        public IReadOnlyCollection<string>? Groups { get; set; }
    }

    /// <summary>
    /// Выпускает JWT RS256, подписанный тестовым ключом. <paramref name="permissions"/> —
    /// сокращённая форма для типичного случая (эквивалент <c>configure: t =&gt; t.Permissions = permissions</c>);
    /// <paramref name="configure"/> позволяет подменить любое из полей <see cref="TokenOptions"/>
    /// (иss/aud/kid/срок/realm_access/чужой клиент) для негативных сценариев.
    /// </summary>
    public static string CreateToken(IReadOnlyCollection<string>? permissions = null, Action<TokenOptions>? configure = null)
    {
        var options = new TokenOptions();

        if (permissions is not null)
        {
            options.Permissions = permissions;
        }

        configure?.Invoke(options);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = options.Subject,
            [JwtRegisteredClaimNames.PreferredUsername] = options.PreferredUsername,
        };

        if (options.Name is not null)
        {
            claims[JwtRegisteredClaimNames.Name] = options.Name;
        }

        var resourceAccess = new Dictionary<string, object>();

        if (options.Permissions is not null)
        {
            resourceAccess[options.Audience] = new Dictionary<string, object> { ["roles"] = options.Permissions.ToArray() };
        }

        if (options.ForeignClientPermissions is not null)
        {
            resourceAccess[options.ForeignClientId] = new Dictionary<string, object> { ["roles"] = options.ForeignClientPermissions.ToArray() };
        }

        if (resourceAccess.Count > 0)
        {
            claims["resource_access"] = resourceAccess;
        }

        if (options.RealmAccessRoles is not null)
        {
            claims["realm_access"] = new Dictionary<string, object> { ["roles"] = options.RealmAccessRoles.ToArray() };
        }

        if (options.Sid is not null)
        {
            claims["sid"] = options.Sid;
        }

        if (options.Nonce is not null)
        {
            claims[JwtRegisteredClaimNames.Nonce] = options.Nonce;
        }

        if (options.Email is not null)
        {
            claims[JwtRegisteredClaimNames.Email] = options.Email;
        }

        if (options.EmailVerified is not null)
        {
            claims["email_verified"] = options.EmailVerified.Value;
        }

        if (options.GivenName is not null)
        {
            claims[JwtRegisteredClaimNames.GivenName] = options.GivenName;
        }

        if (options.FamilyName is not null)
        {
            claims[JwtRegisteredClaimNames.FamilyName] = options.FamilyName;
        }

        if (options.Groups is not null)
        {
            claims["groups"] = options.Groups.ToArray();
        }

        var signingKey = string.Equals(options.Kid, Kid, StringComparison.Ordinal)
            ? SecurityKey
            : new RsaSecurityKey(Key) { KeyId = options.Kid };

        // A scenario that only overrides Expires (e.g. an already-expired
        // token in ErrorHandlingTests) must not fail here
        // just because the default IssuedAt (~now) landed after it: nbf/iat
        // fall back to safely before Expires instead of the untouched default.
        var issuedAt = options.IssuedAt <= options.Expires ? options.IssuedAt : options.Expires - TimeSpan.FromMinutes(1);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = options.Expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256),
            Claims = claims,
        };

        return Handler.CreateEncodedJwt(descriptor);
    }

    /// <summary>
    /// Проставляет Bearer-заголовок. Живёт рядом со способом получить токен
    /// (<see cref="CreateToken"/>) — его зовут все тестовые классы, которым
    /// нужен токен.
    /// </summary>
    public static HttpRequestMessage WithBearer(this HttpRequestMessage request, string accessToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static OpenIdConnectConfiguration BuildConfiguration(string issuer, bool includeKeys)
    {
        // ADR-0022: три адреса добавлены для SessionAuthenticationSetup
        // (AddOpenIdConnect)/RefreshGrantClient/SessionEndpoints.LogoutAsync —
        // они read эти три поля из discovery, а не откуда-то ещё. Harmless для
        // остальных потребителей этого метода (JwtBearerSetup/RealmReadinessMonitor
        // не читают их вовсе). KeycloakTokenEndpointResponder отвечает по этим
        // же путям.
        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = issuer,
            AuthorizationEndpoint = $"{issuer}/protocol/openid-connect/auth",
            TokenEndpoint = $"{issuer}/protocol/openid-connect/token",
            EndSessionEndpoint = $"{issuer}/protocol/openid-connect/logout",
        };

        if (includeKeys)
        {
            configuration.SigningKeys.Add(SecurityKey);
        }

        return configuration;
    }

    /// <summary>
    /// «Ротация ключа realm'а»: discovery-документ с тем
    /// же ключом, что <see cref="Configuration"/>, плюс ещё один <c>kid</c>
    /// поверх того же RSA-ключа. Токен с новым <c>kid</c>
    /// (<c>CreateToken(configure: t =&gt; t.Kid = "…")</c>) подписан тем же
    /// приватным ключом, что и токен со «старым» <c>kid</c> — тест моделирует
    /// появление нового ключа в JWKS realm'а, а не смену секрета.
    /// </summary>
    public static OpenIdConnectConfiguration ConfigurationWithAdditionalKid(string additionalKid)
    {
        var configuration = BuildConfiguration(Issuer, includeKeys: true);
        configuration.SigningKeys.Add(new RsaSecurityKey(Key) { KeyId = additionalKid });
        return configuration;
    }

    /// <summary>
    /// Подменяет <see cref="IConfigurationManager{T}"/> — единая точка
    /// discovery/JWKS для <c>JwtBearer</c> и гейта готовности. Общая
    /// на коллекцию <c>MustaskApiCollection</c> — каждый тест обязан вернуть
    /// режим в <see cref="Mode.Ok"/> в <c>finally</c> (та же дисциплина, что
    /// у <c>KeycloakTokenEndpointResponder</c>).
    /// </summary>
    public sealed class FakeRealmMetadata : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public enum Mode
        {
            /// <summary>Действующий realm: ожидаемый issuer, ключ есть.</summary>
            Ok,

            /// <summary>Discovery недоступен — сеть/таймаут/отказ Keycloak.</summary>
            Unavailable,

            /// <summary>Discovery получен, но issuer не совпадает с ожидаемым.</summary>
            ForeignIssuer,

            /// <summary>Discovery получен, issuer совпал, набор ключей JWKS пуст.</summary>
            EmptyKeys,

            /// <summary>
            /// Discovery не получено вообще ни разу — то, что
            /// бросает настоящий <c>ConfigurationManager&lt;T&gt;</c>
            /// (<see cref="InvalidOperationException"/>, IDX20803), а не
            /// <see cref="HttpRequestException"/>, как <see cref="Unavailable"/>
            /// выше (тот эмулирует сбой уже работавшего realm'а). Даёт
            /// сквозной тест классификации колбэка «discovery не получено →
            /// <c>idp_unavailable</c>» на настоящем типе исключения, а не
            /// только на модульном тесте <c>ClassifyFailure</c>.
            /// </summary>
            NeverObtained,
        }

        private static readonly OpenIdConnectConfiguration ForeignIssuerConfiguration = BuildConfiguration(ForeignIssuer, includeKeys: true);
        private static readonly OpenIdConnectConfiguration EmptyKeysConfiguration = BuildConfiguration(Issuer, includeKeys: false);

        public Mode CurrentMode { get; set; } = Mode.Ok;

        /// <summary>
        /// Подменяет документ, отдаваемый в <see cref="Mode.Ok"/> — нужно
        /// ровно одному сценарию (ротация ключа, <see cref="ConfigurationWithAdditionalKid"/>):
        /// второй запрос обязан увидеть JWKS с новым <c>kid</c>, не трогая
        /// общий статический <see cref="Configuration"/> (он на весь процесс
        /// тестов, а не на один тест). <see langword="null"/> — обычное
        /// поведение, отдаётся <see cref="Configuration"/>.
        /// </summary>
        public OpenIdConnectConfiguration? OkConfigurationOverride { get; set; }

        private OpenIdConnectConfiguration? _okConfigurationAfterRefresh;
        private bool _backgroundRefreshCompleted;

        /// <summary>
        /// Configuration served in
        /// <see cref="Mode.Ok"/> only once <see cref="CompleteBackgroundRefresh"/>
        /// has been called <em>after this property was set</em> — models a
        /// realm key rotation the fake has not "seen" until its background
        /// fetch has actually finished. Unlike the previous shape of this
        /// fake, calling <see cref="RequestRefresh"/> alone is deliberately
        /// not enough: the real <c>ConfigurationManager&lt;OpenIdConnectConfiguration&gt;</c>
        /// (Microsoft.IdentityModel.Protocols.OpenIdConnect 8.19.2) only
        /// schedules that fetch in the background and keeps returning the
        /// old, cached configuration to every <see cref="GetConfigurationAsync"/>
        /// call until it completes (~300 ms later against a real Keycloak,
        /// measured experimentally) — a test that wants the rotated
        /// key visible has to say so explicitly, via
        /// <see cref="CompleteBackgroundRefresh"/>, not merely call
        /// <see cref="RequestRefresh"/>. Scoping "seen a completed refresh"
        /// to the setter (not to the fake's whole lifetime) matters because
        /// this fake is shared across the whole collection
        /// (<see cref="MustaskApiFactory.RealmMetadataFake"/>): an earlier,
        /// unrelated test's <see cref="RequestRefresh"/>/<see cref="CompleteBackgroundRefresh"/>
        /// calls must not make this one see the rotated key too early.
        /// <see langword="null"/> (default) — a refresh changes nothing,
        /// <see cref="OkConfigurationOverride"/> (or <see cref="Configuration"/>)
        /// keeps being served.
        /// </summary>
        public OpenIdConnectConfiguration? OkConfigurationAfterRefresh
        {
            get => _okConfigurationAfterRefresh;
            set
            {
                _okConfigurationAfterRefresh = value;
                _backgroundRefreshCompleted = false;
            }
        }

        /// <summary>How many times <see cref="RequestRefresh"/> was called — evidence that a caller asked for a refresh (and how many times), not that any of them changed what <see cref="GetConfigurationAsync"/> returns.</summary>
        public int RefreshRequestCount { get; private set; }

        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) => CurrentMode switch
        {
            Mode.Ok => Task.FromResult(_backgroundRefreshCompleted && _okConfigurationAfterRefresh is not null
                ? _okConfigurationAfterRefresh
                : OkConfigurationOverride ?? Configuration),
            Mode.ForeignIssuer => Task.FromResult(ForeignIssuerConfiguration),
            Mode.EmptyKeys => Task.FromResult(EmptyKeysConfiguration),
            Mode.Unavailable => Task.FromException<OpenIdConnectConfiguration>(
                new HttpRequestException("Keycloak realm discovery is unavailable (FakeRealmMetadata.Mode.Unavailable).")),
            Mode.NeverObtained => Task.FromException<OpenIdConnectConfiguration>(
                new InvalidOperationException(
                    "IDX20803: Unable to obtain configuration from: '(FakeRealmMetadata.Mode.NeverObtained)'.")),
            _ => throw new InvalidOperationException($"Unknown {nameof(Mode)}: {CurrentMode}."),
        };

        /// <summary>
        /// Counts the call and marks a
        /// background fetch as pending — deliberately does <em>not</em>
        /// change what <see cref="GetConfigurationAsync"/> returns. That is
        /// the whole point of the fix: the real <c>ConfigurationManager</c>
        /// does not update its cache synchronously here either, only once
        /// its own background task completes — modeled by
        /// <see cref="CompleteBackgroundRefresh"/>.
        /// </summary>
        public void RequestRefresh()
        {
            RefreshRequestCount++;
        }

        /// <summary>
        /// The controllable step a test
        /// drives to model the real <c>ConfigurationManager</c>'s background
        /// fetch actually finishing (real-world latency: one discovery +
        /// one JWKS round trip, no relation to how many times
        /// <see cref="RequestRefresh"/> was called). Before this is called,
        /// <see cref="GetConfigurationAsync"/> keeps serving the
        /// pre-rotation configuration no matter how many refreshes were
        /// requested — after it, the configuration set through
        /// <see cref="OkConfigurationAfterRefresh"/> becomes visible. No
        /// real sleep involved; this is what makes the fake deterministic.
        /// </summary>
        public void CompleteBackgroundRefresh()
        {
            _backgroundRefreshCompleted = true;
        }
    }
}
