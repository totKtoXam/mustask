namespace Mustask.Api.IntegrationTests.Features.UserDirectory.Live;

/// <summary>
/// An opt-in live test of the Keycloak provider (mod-014 plan § 10, level L):
/// it builds the stand's Keycloak image and runs it, so it runs only when
/// <c>MUSTASK_KEYCLOAK_PROVIDER_TESTS=1</c> — <c>scripts/verify.sh</c>'s
/// <c>provider</c> bucket sets it; an ordinary test run skips it.
/// </summary>
public sealed class LiveProviderFactAttribute : FactAttribute
{
    public const string Switch = "MUSTASK_KEYCLOAK_PROVIDER_TESTS";

    public LiveProviderFactAttribute()
    {
        if (!IsOn)
        {
            Skip = $"Live Keycloak provider test: set {Switch}=1 (scripts/verify.sh's provider bucket does).";
        }
    }

    public static bool IsOn => Environment.GetEnvironmentVariable(Switch) == "1";
}
