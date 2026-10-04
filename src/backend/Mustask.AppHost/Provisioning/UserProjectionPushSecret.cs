using System.Security.Cryptography;

namespace Mustask.AppHost.Provisioning;

/// <summary>
/// The push channel's shared secret on the stand (ADR-0025 § 3): 32 random
/// bytes, base64url, generated lazily and stable for the life of the one
/// instance <c>AppHost.cs</c> makes per process — the shape of
/// <see cref="KeycloakStandClientSecret"/>. Handed to Keycloak's provider and
/// to Mustask.Api as one secret parameter, which the dashboard masks; never
/// human-set, never logged, so a fresh clone needs no fourth secret (US-6).
/// Both sides restart with the AppHost, so a new value per process is no
/// rotation problem.
/// </summary>
public sealed class UserProjectionPushSecret
{
    private const int SecretBytes = 32;

    private readonly Lazy<string> _value = new(() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_'));

    public string Value => _value.Value;
}
