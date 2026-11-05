using System.Text.Json;

namespace Mustask.Contracts.Tests;

/// <summary>
/// What the realm import must not declare since tech-025 (ADR-0034): the event-listener provider and its credential are
/// gone, the user projection is fed from the sign-in.
/// </summary>
public sealed class KeycloakRealmImportShapeTests
{
    private static string RealmImportPath() =>
        Path.Combine(RepositoryLayout.Root.FullName, "deploy", "keycloak", "realms", "mustask-realm.json");

    private static JsonElement RealmImport()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RealmImportPath()));

        return document.RootElement.Clone();
    }

    [Fact]
    public void RealmImport_EnablesNoUserProjectionProvider()
    {
        var realm = RealmImport();
        var listeners = realm.TryGetProperty("eventsListeners", out var array)
            ? array.EnumerateArray().Select(item => item.GetString()).ToList()
            : [];

        Assert.DoesNotContain("mustask-user-projection", listeners);
        Assert.DoesNotContain("mustask-user-projection", File.ReadAllText(RealmImportPath()), StringComparison.Ordinal);
    }

    [Fact]
    public void RealmImport_HoldsNoPushCredential()
    {
        var text = File.ReadAllText(RealmImportPath());

        Assert.DoesNotContain("push-secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PUSH_SECRET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("UserDirectory", text, StringComparison.Ordinal);
    }

    // D-15: the realm import keeps editUsernameAllowed off (the projection takes the username from the sign-in).
    [Fact]
    public void RealmImport_KeepsUsernameEditingOff()
    {
        Assert.False(RealmImport().TryGetProperty("editUsernameAllowed", out var editUsername) && editUsername.GetBoolean());
    }

    // The panel client must carry the email scope: the projection reads the `email` claim of the id token.
    [Fact]
    public void RealmImport_PanelClientCarriesTheEmailScope()
    {
        var panel = RealmImport().GetProperty("clients").EnumerateArray()
            .Single(client => client.GetProperty("clientId").GetString() == "mustask-panel");
        var scopes = panel.GetProperty("defaultClientScopes").EnumerateArray().Select(item => item.GetString()).ToList();

        Assert.Contains("email", scopes);
        Assert.Contains("profile", scopes);
    }
}
