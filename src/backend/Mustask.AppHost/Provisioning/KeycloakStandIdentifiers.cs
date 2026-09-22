namespace Mustask.AppHost.Provisioning;

/// <summary>
/// The two literals `AppHost.cs` passes into
/// <see cref="KeycloakStandProvisioningOptions"/>, promoted to one named
/// place instead of being typed twice — once here, once (until now) as an
/// inline string in `AppHost.cs`'s event subscriber. The reason this exists
/// at all is `KeycloakStandDisjointnessTests` (`Mustask.Contracts.Tests`): it
/// needs one authoritative value to compare `scripts/keycloak-seed.sh`
/// against, not a copy re-typed by hand that could silently drift from what
/// `AppHost.cs` actually uses.
/// </summary>
public static class KeycloakStandIdentifiers
{
    /// <summary>Realm `mustask`, confidential, service account.</summary>
    public const string ClientId = "mustask-e2e";

    /// <summary>Realm `mustask`, group `/Администраторы`, no password.</summary>
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
}
