using System.Text.RegularExpressions;

namespace Mustask.AppHost.Provisioning;

/// <summary>
/// The literals `AppHost.cs` passes into
/// <see cref="KeycloakStandProvisioningOptions"/>, promoted to one named
/// place instead of being typed twice — once here, once (until now) as an
/// inline string in `AppHost.cs`'s event subscriber. The reason this exists
/// at all is `KeycloakStandDisjointnessTests` (`Mustask.Contracts.Tests`): it
/// needs one authoritative value to compare `scripts/keycloak-seed.sh`
/// against, not a copy re-typed by hand that could silently drift from what
/// `AppHost.cs` actually uses.
/// </summary>
public static partial class KeycloakStandIdentifiers
{
    /// <summary>The <c>keycloak</c> resource's health check that compares the live realm with the import.</summary>
    public const string DriftHealthCheckKey = "keycloak-realm-drift";

    /// <summary>The reset's command, as the drift check's findings name it.</summary>
    public const string ResetCommand = "scripts/stand-reset.sh (PowerShell: scripts/stand-reset.ps1)";

    /// <summary>
    /// The stand's realm when `Keycloak:Realm` is not set (tech-024, ADR-0033: the realm name is configuration, because the
    /// company-wide realm MUSTASK runs in on a server is not called `mustask`).
    /// </summary>
    public const string DefaultRealm = "mustask";

    /// <summary>Environment variable the realm-import file reads its name from (`${MUSTASK_REALM:mustask}`).</summary>
    public const string RealmPlaceholder = "MUSTASK_REALM";

    /// <summary>
    /// The realm name the stand uses: the configured `Keycloak:Realm`, or <see cref="DefaultRealm"/> when it is unset or blank.
    /// The name ends up in URLs and in the import file, so only letters, digits, `-`, `_` and `.` are accepted.
    /// </summary>
    /// <exception cref="InvalidOperationException">The configured name has other characters.</exception>
    public static string RealmName(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultRealm;
        }

        var name = configured.Trim();

        return RealmNamePattern().IsMatch(name)
            ? name
            : throw new InvalidOperationException($"Keycloak:Realm `{name}` is not a realm name: use letters, digits, '-', '_' and '.' only.");
    }

    /// <summary>The API's client: the holder of the four client roles (`Mustask.Domain.Identity.InstanceRoles`).</summary>
    public const string ApiClientId = "mustask-api";

    /// <summary>
    /// The client roles of <see cref="ApiClientId"/> the fixture user is given directly (no group, no realm role). The names are literals
    /// because the AppHost does not reference the domain; `KeycloakStandProvisioningTests` compares them with `InstanceRoles`:
    /// `user` to sign in, `system-admin` for every instance right — what the group «Администраторы» gave before ADR-0033.
    /// </summary>
    public static readonly IReadOnlyList<string> FixtureRoles = ["user", "system-admin"];

    /// <summary>Realm `mustask`, confidential, service account.</summary>
    public const string ClientId = "mustask-e2e";

    /// <summary>The stand's realm, client roles `user` and `system-admin`, no password.</summary>
    public const string FixtureUsername = "e2e-fixture";

    /// <summary>
    /// Realm `mustask`'s `accessTokenLifespan`,
    /// set once by `Mustask.AppHost` with the Keycloak administrator credentials
    /// it already holds — not by `mustask-e2e`, which deliberately lacks
    /// `manage-realm`/`view-realm` and stays that way (widening it for test
    /// speed is rejected). 120, not the specs' original 90–120s range: long
    /// enough that the panel's 60s renewal lead
    /// (`RENEW_LEAD_MS`, `shared/api/session.ts`) still leaves a real window
    /// before expiry, short enough that specs 6/9/11 observe a renewal
    /// without waiting out the production 900s. The suite's own waits are
    /// calibrated to this exact number — changing it here is a contract
    /// change with the E2E suite, not a free tuning knob.
    /// </summary>
    public const int AccessTokenLifespanSeconds = 120;

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
    private static partial Regex RealmNamePattern();
}
