using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Mustask.AppHost.Provisioning;

namespace Mustask.Api.IntegrationTests.Features.Realm.Live;

/// <summary>
/// mod-016 plan-backend § 4.3, § 4.7: the stand's Keycloak image (built from <c>deploy/keycloak/Dockerfile</c>) started with a
/// <b>realm file of the test's choosing</b> - the repository's real import - and the stand's placeholders filled in, so that what
/// Keycloak does with the file is what a stand's Keycloak does. One container per test, on a random host port; disposing it
/// removes it.
/// </summary>
internal sealed class LiveRealmContainer : IAsyncDisposable
{
    /// <summary>The panel client's secret the import's <c>${MUSTASK_PANEL_CLIENT_SECRET}</c> resolves to.</summary>
    public const string PanelSecret = "live-realm-panel-secret-not-real-0123456789";

    internal const string ImageTag = "mustask-keycloak-live:test";

    private readonly IContainer _container;

    private LiveRealmContainer(IContainer container, HttpClient http, string adminPassword, string realm)
    {
        _container = container;
        Http = http;
        AdminPassword = adminPassword;
        Realm = realm;
    }

    public HttpClient Http { get; }

    public string AdminPassword { get; }

    /// <summary>The realm the container was started for (<c>MUSTASK_REALM</c>, default <c>mustask</c>).</summary>
    public string Realm { get; }

    /// <summary>The stand's placeholders: the values the container resolved the import with.</summary>
    public static IReadOnlyDictionary<string, string> Placeholders { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MUSTASK_PANEL_CLIENT_SECRET"] = PanelSecret,
    };

    public static async Task<LiveRealmContainer> StartAsync(string realmJson, string realm = "mustask")
    {
        BuildImage();

        var adminPassword = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12));
        var container = new ContainerBuilder(ImageTag)
            .WithCommand("start-dev", "--import-realm")
            .WithResourceMapping(Encoding.UTF8.GetBytes(realmJson), $"/opt/keycloak/data/import/{realm}-realm.json")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", adminPassword)
            .WithEnvironment("MUSTASK_PANEL_CLIENT_SECRET", PanelSecret)
            .WithEnvironment("MUSTASK_REALM", realm)
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath($"/realms/{realm}")))
            .Build();

        await container.StartAsync();

        var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{container.GetMappedPublicPort(8080)}") };

        return new LiveRealmContainer(container, http, adminPassword, realm);
    }

    internal static void BuildImage()
    {
        var context = Path.Combine(FindRepositoryRoot(), "deploy", "keycloak");
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in new[] { "build", "-q", "-t", ImageTag, context })
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["DOCKER_BUILDKIT"] = "1";

        using var process = System.Diagnostics.Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("docker build of deploy/keycloak failed: " + error.Result);
        }
    }

    private static string FindRepositoryRoot() => LiveRealmImportTests.RepositoryRoot();

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
        using var request = new HttpRequestMessage(method, $"/admin/realms/{Realm}" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync());

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();

        return ((int)response.StatusCode, string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text), response.Headers.Location);
    }

    /// <summary>A user in realm <c>mustask</c> with a profile that lets them sign in and the named client roles of <c>mustask-api</c> given directly; answers the user's id.</summary>
    public async Task<string> CreateUserAsync(string username, params string[] clientRoles)
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

        if (clientRoles.Length > 0)
        {
            var (_, clients, _) = await AdminAsync(HttpMethod.Get, "/clients?clientId=mustask-api");
            var apiId = (string)clients![0]!["id"]!;
            var (_, allRoles, _) = await AdminAsync(HttpMethod.Get, $"/clients/{apiId}/roles");
            var wanted = new JsonArray(clientRoles
                .Select(name => ((JsonArray)allRoles!).Single(role => (string)role!["name"]! == name)!)
                .Select(role => (JsonNode)new JsonObject { ["id"] = (string)role["id"]!, ["name"] = (string)role["name"]! })
                .ToArray());
            var (put, _, _) = await AdminAsync(HttpMethod.Post, $"/users/{id}/role-mappings/clients/{apiId}", wanted);
            Assert.True(put == 204, $"giving {username} the roles {string.Join(",", clientRoles)} answered {put}");
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
                Import = KeycloakRealmImport.Load(realmsDirectory, Realm, new Dictionary<string, string>(Placeholders, StringComparer.Ordinal) { ["MUSTASK_REALM"] = Realm }),
                AdminUsername = "admin",
                AdminPassword = AdminPassword,
                SecretClientId = "mustask-panel",
                SecretClientSecret = PanelSecret,
            },
            CancellationToken.None);
    }
}
