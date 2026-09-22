using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mustask.Api.Authentication.Readiness;
using Mustask.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Mustask.Api.IntegrationTests.Features.Authorization;

/// <summary>
/// Вместо снесённого <c>JwtNegativeEndpointTests</c>, который проверял
/// собственный контур identity: <c>Mustask.Api</c>
/// принимает только токены своего realm'а, подписанные по JWKS, а не по
/// ключу в конфигурации. Живого Keycloak нет — discovery/JWKS подменяет
/// <see cref="MustaskApiFactory.RealmMetadataFake"/> (общая на коллекцию, режим
/// возвращается в <c>Ok</c> в <c>finally</c> там, где переключается).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class BearerTokenValidationTests
{
    private const string ProtectedRoute = "/api/dictionaries/languages";

    private readonly MustaskApiFactory _factory;

    public BearerTokenValidationTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpResponseMessage> CallProtectedRouteAsync(string? token)
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, ProtectedRoute);

        if (token is not null)
        {
            request.WithBearer(token);
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task NoToken_ReturnsUnauthorizedWithErrorCode()
    {
        var response = await CallProtectedRouteAsync(null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("http.unauthorized", body.GetProperty("errorCode").GetString());
    }

    /// <summary>Классическая algorithm-confusion атака: сервер публикует RS256 публичный ключ, а не принимает его же как секрет HS256.</summary>
    [Fact]
    public async Task Hs256TokenSignedWithRealmsPublicKeyAsSecret_ReturnsUnauthorized()
    {
        var publicKeyBytes = Encoding.UTF8.GetBytes(RealmPublicKeyPem());
        var now = DateTimeOffset.UtcNow;
        var credentials = new SigningCredentials(new SymmetricSecurityKey(publicKeyBytes), SecurityAlgorithms.HmacSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = KeycloakTestRealm.Issuer,
            Audience = KeycloakTestRealm.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(15).UtcDateTime,
            SigningCredentials = credentials,
        };
        var handler = new JwtSecurityTokenHandler();
        var token = (JwtSecurityToken)handler.CreateToken(descriptor);
        token.Header["kid"] = KeycloakTestRealm.Kid; // выдаёт себя за настоящий ключ realm'а
        var forgedToken = handler.WriteToken(token);

        var response = await CallProtectedRouteAsync(forgedToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Ручная сборка: JwtSecurityTokenHandler не даёт выпустить alg:none — именно это и есть суть атаки.</summary>
    [Fact]
    public async Task AlgNoneToken_ReturnsUnauthorized()
    {
        var now = DateTimeOffset.UtcNow;
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            iss = KeycloakTestRealm.Issuer,
            aud = KeycloakTestRealm.Audience,
            sub = Guid.NewGuid().ToString(),
            iat = now.ToUnixTimeSeconds(),
            nbf = now.ToUnixTimeSeconds(),
            exp = now.AddMinutes(15).ToUnixTimeSeconds(),
        })));
        var algNoneToken = $"{header}.{payload}.";

        var response = await CallProtectedRouteAsync(algNoneToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Подделка: чужая пара ключей, но с настоящим kid realm'а — подпись не совпадает.</summary>
    [Fact]
    public async Task ForeignKeyClaimingTheRealKid_ReturnsUnauthorized()
    {
        using var foreignKey = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var credentials = new SigningCredentials(new RsaSecurityKey(foreignKey), SecurityAlgorithms.RsaSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = KeycloakTestRealm.Issuer,
            Audience = KeycloakTestRealm.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(15).UtcDateTime,
            SigningCredentials = credentials,
        };
        var handler = new JwtSecurityTokenHandler();
        var token = (JwtSecurityToken)handler.CreateToken(descriptor);
        token.Header["kid"] = KeycloakTestRealm.Kid;
        var forgedToken = handler.WriteToken(token);

        var response = await CallProtectedRouteAsync(forgedToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredBeyondClockSkew_ReturnsUnauthorized()
    {
        var token = KeycloakTestRealm.CreateToken(configure: t => t.Expires = DateTimeOffset.UtcNow.AddSeconds(-45));

        var response = await CallProtectedRouteAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Допуск расхождения часов — 30 секунд, значит токен, просроченный на 10 секунд, всё ещё принимается.</summary>
    [Fact]
    public async Task ExpiredWithinClockSkew_IsStillAccepted()
    {
        var token = KeycloakTestRealm.CreateToken(
            [PermissionCatalog.DictionariesLanguagesRead],
            configure: t => t.Expires = DateTimeOffset.UtcNow.AddSeconds(-10));

        var response = await CallProtectedRouteAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForeignIssuer_ReturnsUnauthorized()
    {
        var token = KeycloakTestRealm.CreateToken(configure: t => t.Issuer = KeycloakTestRealm.ForeignIssuer);

        var response = await CallProtectedRouteAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AudienceWithoutMustaskApi_ReturnsUnauthorized()
    {
        var token = KeycloakTestRealm.CreateToken(configure: t => t.Audience = "some-other-client");

        var response = await CallProtectedRouteAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownKid_ReturnsUnauthorized()
    {
        var token = KeycloakTestRealm.CreateToken(configure: t => t.Kid = "kid-not-in-any-realm-keyset");

        var response = await CallProtectedRouteAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Ротация ключа НЕ делает первый запрос новым
    /// <c>kid</c> успешным — окно неготовности документировано, а не
    /// дефект. Первый запрос: 401 (ключа ещё нет в JWKS). Затем ключ
    /// появляется (<see cref="KeycloakTestRealm.ConfigurationWithAdditionalKid"/>),
    /// монитор дёргает <c>CheckAsync()</c> (то же самое, что происходит в
    /// его фоновом цикле раз в 30 с) — тот же токен: 200.
    /// </summary>
    [Fact]
    public async Task KeyRotation_FirstRequestUnauthorized_ThenOkAfterMonitorPicksUpTheNewKey()
    {
        var newKid = Guid.CreateVersion7().ToString("N");
        var token = KeycloakTestRealm.CreateToken(
            [PermissionCatalog.DictionariesLanguagesRead],
            configure: t => t.Kid = newKid);

        try
        {
            var firstResponse = await CallProtectedRouteAsync(token);
            Assert.Equal(HttpStatusCode.Unauthorized, firstResponse.StatusCode);

            _factory.RealmMetadataFake.OkConfigurationOverride = KeycloakTestRealm.ConfigurationWithAdditionalKid(newKid);
            using (var scope = _factory.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<RealmReadinessMonitor>().CheckAsync();
            }

            var secondResponse = await CallProtectedRouteAsync(token);
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        }
        finally
        {
            _factory.RealmMetadataFake.OkConfigurationOverride = null;
        }
    }

    private static string RealmPublicKeyPem()
    {
        var jwk = (RsaSecurityKey)KeycloakTestRealm.Configuration.SigningKeys.Single();
        return jwk.Rsa!.ExportSubjectPublicKeyInfoPem();
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
