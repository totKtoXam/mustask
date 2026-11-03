namespace Mustask.Api.IntegrationTests.Features.Realm.Live;

/// <summary>
/// An opt-in live test against a real Keycloak (the stand's image started with the repository's realm import): it
/// builds the image and runs it, so it runs only when <c>MUSTASK_KEYCLOAK_LIVE_TESTS=1</c> - <c>scripts/verify.sh</c>'s
/// <c>keycloak</c> bucket sets it; an ordinary test run skips it.
/// </summary>
public sealed class LiveRealmFactAttribute : FactAttribute
{
    public const string Switch = "MUSTASK_KEYCLOAK_LIVE_TESTS";

    public LiveRealmFactAttribute()
    {
        if (!IsOn)
        {
            Skip = $"Live Keycloak realm test: set {Switch}=1 (scripts/verify.sh's keycloak bucket does).";
        }
    }

    public static bool IsOn => Environment.GetEnvironmentVariable(Switch) == "1";
}
