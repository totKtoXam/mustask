using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Mustask.Api.Authentication;

/// <summary>
/// Конфигурация подключения к realm'у Keycloak: адрес,
/// ожидаемые <c>iss</c>/<c>aud</c> и требование HTTPS у discovery-документа.
/// Значения читаются один раз, из <see cref="IConfiguration"/> до
/// <c>WebApplicationBuilder.Build()</c> — единственный
/// механизм отказа старта при незаданном ключе; <c>ValidateOnStart()</c>
/// и <c>IValidateOptions&lt;T&gt;</c> сознательно не заводятся.
/// </summary>
public sealed class KeycloakOptions
{
    /// <summary>Адрес realm'а без завершающего "/", например "http://localhost:5010/realms/mustask".</summary>
    public required string Authority { get; init; }

    /// <summary>Ожидаемое значение в <c>aud</c>, например "mustask-api".</summary>
    public required string Audience { get; init; }

    /// <summary>Ожидаемый <c>iss</c>; по умолчанию равен <see cref="Authority"/>.</summary>
    public required string Issuer { get; init; }

    /// <summary>Требовать ли HTTPS у адреса discovery.</summary>
    public required bool RequireHttpsMetadata { get; init; }

    /// <summary>
    /// Читает и проверяет <c>Keycloak:*</c> из конфигурации. Отсутствие
    /// обязательного ключа (<see cref="Authority"/>/<see cref="Audience"/>)
    /// валит старт с сообщением, называющим ключ.
    /// </summary>
    public static KeycloakOptions FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var authority = (configuration["Keycloak:Authority"]
                ?? throw new InvalidOperationException("Missing configuration value Keycloak:Authority."))
            .TrimEnd('/');

        var audience = configuration["Keycloak:Audience"]
            ?? throw new InvalidOperationException("Missing configuration value Keycloak:Audience.");

        var issuer = configuration["Keycloak:Issuer"];
        if (string.IsNullOrEmpty(issuer))
        {
            issuer = authority;
        }

        // Явное значение в конфигурации побеждает умолчание по среде в обе
        // стороны (тот же принцип, что у ApiExplorer:Enabled,
        // docs/conventions/backend.md).
        var requireHttpsMetadata = configuration.GetValue<bool?>("Keycloak:RequireHttpsMetadata")
            ?? !environment.IsDevelopment();

        return new KeycloakOptions
        {
            Authority = authority,
            Audience = audience,
            Issuer = issuer,
            RequireHttpsMetadata = requireHttpsMetadata,
        };
    }
}
