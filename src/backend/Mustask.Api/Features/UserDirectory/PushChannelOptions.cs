using Microsoft.Extensions.Configuration;

namespace Mustask.Api.Features.UserDirectory;

/// <summary>
/// The push channel's credential (ADR-0025 § 3; BR-UP-10): a secret shared
/// only by the Keycloak provider and MUSTASK, from environment variables or
/// user secrets only. Read once from configuration before
/// <c>WebApplicationBuilder.Build()</c> — the <c>KeycloakOptions</c>
/// pattern: a missing or short secret fails the start naming the key, never
/// the value.
/// </summary>
public sealed class PushChannelOptions
{
    public const string SecretKey = "UserDirectory:Push:Secret";

    public const string PreviousSecretKey = "UserDirectory:Push:PreviousSecret";

    /// <summary>The shortest secret accepted, on both sides (api-contract.md, Part D).</summary>
    public const int MinimumSecretLength = 32;

    /// <summary>The secret requests are signed with.</summary>
    public required string Secret { get; init; }

    /// <summary>Accepted too, during a rotation (ADR-0025 § 3); null when not set.</summary>
    public string? PreviousSecret { get; init; }

    public static PushChannelOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var secret = configuration[SecretKey];

        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException(
                $"Missing configuration value {SecretKey}: the user directory's push channel needs its shared secret "
                + "(an environment variable or user secrets; on the stand Mustask.AppHost generates it).");
        }

        EnsureLongEnough(SecretKey, secret);

        var previous = configuration[PreviousSecretKey];

        if (string.IsNullOrEmpty(previous))
        {
            previous = null;
        }
        else
        {
            EnsureLongEnough(PreviousSecretKey, previous);
        }

        return new PushChannelOptions { Secret = secret, PreviousSecret = previous };
    }

    private static void EnsureLongEnough(string key, string value)
    {
        if (value.Length < MinimumSecretLength)
        {
            throw new InvalidOperationException(
                $"Configuration value {key} is shorter than {MinimumSecretLength} characters.");
        }
    }
}
