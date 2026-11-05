using Mustask.Api.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Mustask.Api.Authentication;

/// <summary>
/// <c>Mustask.Api</c> как resource server realm'а Keycloak (ADR-0018).
/// Проверка Bearer-токена — по JWKS realm'а через общий
/// <see cref="IConfigurationManager{T}"/> (см. <see cref="Readiness.RealmMetadata"/>),
/// права — <see cref="KeycloakPermissionClaims"/> в <c>OnTokenValidated</c>.
/// Авторизация: политики по правам + deny-all fallback.
/// </summary>
public static class JwtBearerSetup
{
    public static IServiceCollection AddMustaskAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<KeycloakOptions, IConfigurationManager<OpenIdConnectConfiguration>, IHostEnvironment>(
                (bearerOptions, keycloak, configurationManager, environment) =>
                {
                    // Claim-типы остаются как в токене (preferred_username,
                    // name, resource_access), а не превращаются в длинные
                    // ClaimTypes.*-URI — упрощает чтение
                    // claims в HttpContextCurrentUser и в OnTokenValidated
                    // ниже. Это условие, при котором
                    // resource_access вообще приезжает одним JSON-claim'ом, а
                    // не расплющенным набором — менять нельзя.
                    bearerOptions.MapInboundClaims = false;

                    // Общая точка discovery/JWKS с гейтом готовности —
                    // один поход за метаданными на весь процесс, а не
                    // отдельный кэш JwtBearer plus отдельный кэш монитора.
                    bearerOptions.ConfigurationManager = configurationManager;

                    // Вне Development WWW-Authenticate не несёт
                    // error/error_description — подробности проверки токена
                    // не публикуются.
                    bearerOptions.IncludeErrorDetails = environment.IsDevelopment();

                    bearerOptions.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = keycloak.Issuer,
                        ValidateAudience = true,
                        ValidAudience = keycloak.Audience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        RequireSignedTokens = true,
                        RequireExpirationTime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        // Жёстко только RS256: alg:none и симметричные алгоритмы (HS256
                        // на публичном ключе как секрете) отвергаются.
                        // Ключи для проверки подписи — из bearerOptions.ConfigurationManager
                        // (JWKS realm'а), не из конфигурации приложения.
                        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                        // Ротация ключа: токен с
                        // "kid", которого нет в JWKS, обязан провалиться —
                        // именно это и есть окно неготовности, закрываемое
                        // RefreshOnIssuerKeyNotFound на СЛЕДУЮЩЕМ запросе, а
                        // не эту проверку. Библиотечное умолчание (true)
                        // пробует ЛЮБОЙ зарегистрированный ключ, если "kid"
                        // не совпал ни с одним, — это разумно для реального
                        // Keycloak (там ключи разные), но означает, что при
                        // ротации токен с ещё не подхваченным "kid" был бы
                        // принят как валидный, если бы совпадение подписи
                        // случайно нашлось (например, при повторном
                        // использовании конфигурации у двух ключей realm'а с
                        // одинаковым материалом) — резолюция обязана идти
                        // строго по "kid".
                        TryAllIssuerSigningKeys = false,
                        NameClaimType = "name",
                    };

                    bearerOptions.Events = new JwtBearerEvents
                    {
                        // Keycloak-токен несёт свои обязательные claim'ы,
                        // которые проверяет библиотека через
                        // TokenValidationParameters — отдельной ручной
                        // проверки не требуется (в отличие от
                        // прежнего собственного токена). Единственное, что
                        // добавляем сами, — права из resource_access.
                        OnTokenValidated = context =>
                        {
                            if (context.Principal is not null)
                            {
                                KeycloakPermissionClaims.Append(context.Principal, keycloak.Audience);
                            }

                            return Task.CompletedTask;
                        },
                    };
                });

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicies>();

        // mod-016 (ADR-0027): the organization-scoped check. Scoped, because it reads the request's database context.
        services.AddScoped<IAuthorizationHandler, OrganizationPermissionHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy(PermissionPolicies.AuthenticatedOnlyPolicyName, policy => policy.RequireAuthenticatedUser())
            // Deny-all — недостижимое требование делает Forbid
            // неизбежным для ЛЮБОГО аутентифицированного пользователя без
            // явной разметки; анонимный запрос получает Challenge (401)
            // раньше, чем до assertion дойдёт очередь.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(_ => false)
                .Build());

        return services;
    }
}
