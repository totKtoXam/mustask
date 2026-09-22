using Microsoft.Extensions.Http;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mustask.Api.Authentication.Readiness;

/// <summary>
/// Фабрика <see cref="IConfigurationManager{T}"/> для discovery-документа
/// realm'а — общая точка для <c>JwtBearerSetup</c> и
/// <see cref="RealmReadinessMonitor"/>: один поход за
/// discovery/JWKS, а не два независимых кэша.
/// </summary>
public static class RealmMetadata
{
    /// <summary>Имя именованного <see cref="HttpClient"/> из <see cref="IHttpClientFactory"/>.</summary>
    public const string HttpClientName = "keycloak-discovery";

    /// <summary>Таймаут запроса discovery/JWKS.</summary>
    public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Строго меньше периода цикла <see cref="RealmReadinessMonitor"/>
    /// (30 с) — иначе дроссель <c>RequestRefresh()</c> съедал бы тик
    /// монитора и окно неготовности при ротации ключа удваивалось.
    /// </summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

    public static IConfigurationManager<OpenIdConnectConfiguration> Create(
        KeycloakOptions options, IHttpClientFactory httpClientFactory)
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        httpClient.Timeout = HttpTimeout;

        return new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{options.Authority}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(httpClient) { RequireHttps = options.RequireHttpsMetadata })
        {
            RefreshInterval = RefreshInterval,
        };
    }
}
