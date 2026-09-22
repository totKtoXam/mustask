using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Mustask.Api.Features.Session;

/// <summary>
/// Configuration the session module needs on top of the already-registered
/// <see cref="Mustask.Api.Authentication.KeycloakOptions"/>.
/// <c>Authority</c> is deliberately not duplicated here — it is read
/// straight off the shared <see cref="Mustask.Api.Authentication.KeycloakOptions"/>
/// instance already in DI, so there is exactly one place that parses that
/// value.
/// </summary>
public sealed class SessionOidcOptions
{
    /// <summary>
    /// Fixed by the realm import (<c>deploy/keycloak/realms/mustask-realm.json</c>),
    /// not business data — a Named Value constant next to the concept that
    /// owns it (docs/conventions/backend.md, "Named values").
    /// </summary>
    public const string ClientId = "mustask-panel";

    /// <summary>Secret of the confidential client <c>mustask-panel</c> (ADR-0022 Decision 4) — never in code, never in a committed file.</summary>
    public required string ClientSecret { get; init; }

    /// <summary>
    /// Absolute address of the panel, without a trailing slash. Every
    /// redirect this module builds is <c>{PanelUrl}{returnUrl}</c>
    /// — the panel and <c>Mustask.Api</c> are different
    /// origins, so a relative <c>Location</c> would send the browser back to
    /// <c>Mustask.Api</c>'s own origin instead.
    /// </summary>
    public required string PanelUrl { get; init; }

    /// <summary>
    /// Values that must never reach the confidential
    /// client's secret outside Development — the realm import's own
    /// placeholder (<c>deploy/keycloak/realms/mustask-realm.json</c>, no default
    /// value any more) and the literal Keycloak would end up with if a
    /// deployment forgot to substitute <c>MUSTASK_PANEL_CLIENT_SECRET</c> before
    /// the very first import (Keycloak has no mechanism to fail on a missing
    /// environment variable of its own).
    /// </summary>
    private static readonly string[] KnownPlaceholderSecrets =
    [
        "dev-only-placeholder-secret",
        "${MUSTASK_PANEL_CLIENT_SECRET}",
    ];

    /// <summary>Below this, a secret is not "short" — it does not deserve the name at all.</summary>
    private const int MinimumClientSecretLength = 32;

    /// <summary>
    /// Reads <c>Keycloak:ClientSecret</c>/<c>Session:PanelUrl</c> — same
    /// fail-fast convention as <see cref="Mustask.Api.Authentication.KeycloakOptions.FromConfiguration"/>:
    /// a missing key stops the host at startup, naming the key.
    /// </summary>
    public static SessionOidcOptions FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var clientSecret = configuration["Keycloak:ClientSecret"]
            ?? throw new InvalidOperationException("Missing configuration value Keycloak:ClientSecret.");

        if (!environment.IsDevelopment())
        {
            // Удалённого значения по умолчанию из
            // realm-импорта мало — незаменённый плейсхолдер тоже известное
            // значение. Точка контроля здесь: старт отказывает, обмен кода
            // падает с unauthorized_client, утёкший секрет выкупать нечего.
            if (KnownPlaceholderSecrets.Contains(clientSecret, StringComparer.Ordinal)
                || clientSecret.Length < MinimumClientSecretLength)
            {
                throw new InvalidOperationException(
                    "Keycloak:ClientSecret is a known placeholder or shorter than "
                    + $"{MinimumClientSecretLength} characters outside Development. Set MUSTASK_PANEL_CLIENT_SECRET "
                    + "before the first realm import, or regenerate the mustask-panel client secret in the Keycloak "
                    + "console and update Keycloak:ClientSecret to match.");
            }
        }

        var panelUrl = (configuration["Session:PanelUrl"]
                ?? throw new InvalidOperationException("Missing configuration value Session:PanelUrl."))
            .TrimEnd('/');

        return new SessionOidcOptions
        {
            ClientSecret = clientSecret,
            PanelUrl = panelUrl,
        };
    }
}
