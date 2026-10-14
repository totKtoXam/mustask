using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Mustask.AppHost.Provisioning;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory.Live;

/// <summary>
/// mod-016 plan-backend § 4.3, § 4.7: the stand's Keycloak image (built from <c>deploy/keycloak/Dockerfile</c>, the provider inside)
/// started with a <b>realm file of the test's choosing</b> — the repository's real import, or the pre-mod-016 one — and the stand's
/// placeholders filled in, so that what Keycloak does with the file is what a stand's Keycloak does. Unlike
/// <see cref="LiveKeycloakFixture"/> it needs no receiver for the provider's pushes (the provider is pointed at nothing that answers):
/// these tests are about the realm, not the projection. One container per test, on a random host port; disposing it removes it.
/// </summary>
internal sealed class LiveRealmContainer : IAsyncDisposable
{
    /// <summary>The panel client's secret the import's <c>${MUSTASK_PANEL_CLIENT_SECRET}</c> resolves to.</summary>
    public const string PanelSecret = "live-realm-panel-secret-not-real-0123456789";

    private readonly IContainer _container;

    private LiveRealmContainer(IContainer container, HttpClient http, string adminPassword)
    {
        _container = container;
        Http = http;
        AdminPassword = adminPassword;
    }

    public HttpClient Http { get; }

    public string AdminPassword { get; }

    /// <summary>The stand's placeholders: the values the container resolved the import with.</summary>
    public static IReadOnlyDictionary<string, string> Placeholders { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MUSTASK_PANEL_CLIENT_SECRET"] = PanelSecret,
    };

    public static async Task<LiveRealmContainer> StartAsync(string realmJson)
    {
        LiveKeycloakFixture.BuildImage();

        var adminPassword = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12));
        var container = new ContainerBuilder(LiveKeycloakFixture.ImageTag)
            .WithCommand("start-dev", "--import-realm")
            .WithResourceMapping(Encoding.UTF8.GetBytes(realmJson), "/opt/keycloak/data/import/mustask-realm.json")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", adminPassword)
            .WithEnvironment("MUSTASK_PANEL_CLIENT_SECRET", PanelSecret)

            // The realm lists the provider as an event listener; it needs a destination and a secret to start, and nothing there.
            .WithEnvironment("KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__ENDPOINT", "http://127.0.0.1:9")
            .WithEnvironment("KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__PUSH_SECRET", Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24)))
            .WithEnvironment("KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__REQUIRE_HTTPS", "false")
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath("/realms/mustask")))
            .Build();

        await container.StartAsync();

        var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{container.GetMappedPublicPort(8080)}") };

        return new LiveRealmContainer(container, http, adminPassword);
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await _container.DisposeAsync();
    }

    /// <summary>An admin token of the master realm.</summary>
    public async Task<string> AdminTokenAsync()
    {
        using var response = await Http.PostAsync("/realms/master/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "admin-cli",
            ["grant_type"] = "password",
            ["username"] = "admin",
            ["password"] = AdminPassword,
        }));
        response.EnsureSuccessStatusCode();

        return (string)(await response.Content.ReadFromJsonAsync<JsonObject>())!["access_token"]!;
    }

    /// <summary>One admin REST request of realm <c>mustask</c>; answers the status and the parsed body.</summary>
    public async Task<(int Status, JsonNode? Body, Uri? Location)> AdminAsync(HttpMethod method, string path, JsonNode? body = null)
    {
        using var request = new HttpRequestMessage(method, "/admin/realms/mustask" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync());

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        return ((int)response.StatusCode, string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text), response.Headers.Location);
    }

    /// <summary>A user in realm <c>mustask</c> with a profile that lets them sign in; answers the user's id.</summary>
    public async Task<string> CreateUserAsync(string username, params string[] groupPaths)
    {
        var (status, _, location) = await AdminAsync(HttpMethod.Post, "/users", new JsonObject
        {
            ["username"] = username,
            ["enabled"] = true,
            ["emailVerified"] = true,
            ["email"] = $"{username}@live.test",
            ["firstName"] = "Живой",
            ["lastName"] = username,
        });
        Assert.True(status == 201, $"creating {username} answered {status}");
        var id = location!.Segments[^1];

        foreach (var path in groupPaths)
        {
            var (groupStatus, group, _) = await AdminAsync(HttpMethod.Get, "/group-by-path/" + string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString)));
            Assert.True(groupStatus == 200, $"group {path} answered {groupStatus}");
            var (put, _, _) = await AdminAsync(HttpMethod.Put, $"/users/{id}/groups/{group!["id"]}");
            Assert.True(put == 204, $"putting {username} into {path} answered {put}");
        }

        return id;
    }

    /// <summary>
    /// The access token Keycloak would issue the panel client for the user — evaluated by Keycloak itself with the realm's scopes and
    /// mappers (the admin REST "generate example access token"), so <c>resource_access</c> is what a sign-in would carry.
    /// </summary>
    public async Task<JsonNode> ExampleAccessTokenAsync(string userId)
    {
        var (_, clients, _) = await AdminAsync(HttpMethod.Get, "/clients?clientId=mustask-panel");
        var panelId = (string)clients![0]!["id"]!;
        var (status, token, _) = await AdminAsync(HttpMethod.Get, $"/clients/{panelId}/evaluate-scopes/generate-example-access-token?scope=openid&userId={userId}");
        Assert.True(status == 200, $"evaluating the token answered {status}");

        return token!;
    }

    /// <summary>The <c>mustask-api</c> roles in a user's token, sorted.</summary>
    public async Task<string[]> ApiRolesOfAsync(string userId)
    {
        var token = await ExampleAccessTokenAsync(userId);

        return ((JsonArray?)token["resource_access"]?["mustask-api"]?["roles"])?.Select(role => (string)role!).Order(StringComparer.Ordinal).ToArray() ?? [];
    }

    /// <summary>The stand's drift check against this container, with <paramref name="importFile"/> as the expectation.</summary>
    public async Task<IReadOnlyList<string>> DriftFindingsAsync(string realmsDirectory)
    {
        return await KeycloakStandDriftCheck.RunAsync(
            Http,
            new KeycloakDriftCheckOptions
            {
                Import = KeycloakRealmImport.Load(realmsDirectory, "mustask", Placeholders),
                AdminUsername = "admin",
                AdminPassword = AdminPassword,
                SecretClientId = "mustask-panel",
                SecretClientSecret = PanelSecret,
            },
            CancellationToken.None);
    }
}
