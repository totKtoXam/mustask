using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory.Live;

/// <summary>
/// The stand's Keycloak image — built from <c>deploy/keycloak/Dockerfile</c>,
/// so the provider inside is exactly what the stand runs — started with a
/// throwaway realm <c>mustask</c> of its own (the listener enabled,
/// <c>editUsernameAllowed</c> on, a client with a service account) and the
/// provider pointed at a <see cref="SignatureCheckingReceiver"/> on the host.
/// Only when <see cref="LiveProviderFactAttribute.IsOn"/>.
/// </summary>
public sealed class LiveKeycloakFixture : IAsyncLifetime
{
    public const string ServiceAccountClientId = "live-service-client";

    private const string ImageTag = "mustask-keycloak-provider-live:test";

    /// <summary>How long the fixture waits for the first push (<see cref="FirstPushAfterReady"/>); well past any bound a test asserts.</summary>
    public static readonly TimeSpan StartWindowWait = TimeSpan.FromSeconds(60);

    private readonly string _pushSecret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));

    private readonly string _adminPassword = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12));

    private IContainer? _keycloak;

    internal SignatureCheckingReceiver Receiver { get; private set; } = null!;

    internal string PushSecret => _pushSecret;

    public HttpClient Http { get; private set; } = null!;

    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>
    /// How long the push of a user created the moment Keycloak answered took
    /// to arrive — measured here, before any test runs, because which test
    /// runs first is not fixed (code review F13: the provider holds every
    /// change until it knows its listener is enabled). Null when it did not
    /// arrive within <see cref="StartWindowWait"/>.
    /// </summary>
    public TimeSpan? FirstPushAfterReady { get; private set; }

    public async Task InitializeAsync()
    {
        if (!LiveProviderFactAttribute.IsOn)
        {
            return;
        }

        BuildImage();

        Receiver = new SignatureCheckingReceiver(_pushSecret);
        await Receiver.StartAsync();
        await TestcontainersSettings.ExposeHostPortsAsync((ushort)Receiver.Port);

        _keycloak = new ContainerBuilder(ImageTag)
            .WithCommand("start-dev", "--import-realm")
            .WithResourceMapping(Encoding.UTF8.GetBytes(RealmImport()), "/opt/keycloak/data/import/mustask-realm.json")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", _adminPassword)
            .WithEnvironment("KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__ENDPOINT", $"http://host.testcontainers.internal:{Receiver.Port}")
            .WithEnvironment("KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__PUSH_SECRET", _pushSecret)
            .WithEnvironment("KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__REQUIRE_HTTPS", "false")
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath("/realms/mustask")))
            .Build();

        await _keycloak.StartAsync();
        StartedAt = DateTimeOffset.UtcNow;

        Http = new HttpClient { BaseAddress = new Uri($"http://localhost:{_keycloak.GetMappedPublicPort(8080)}") };

        await MeasureTheFirstPushAsync();
    }

    private async Task MeasureTheFirstPushAsync()
    {
        var (id, _) = await CreateUserAsync(new JsonObject
        {
            ["username"] = "live.first",
            ["firstName"] = "Первый",
            ["lastName"] = "Готов",
            ["email"] = "live.first@example.test",
            ["enabled"] = true,
        });
        var sinceTheChange = Stopwatch.StartNew();

        try
        {
            await Receiver.WaitForEntryAsync(id, _ => true, StartWindowWait);
            FirstPushAfterReady = sinceTheChange.Elapsed;
        }
        catch (TimeoutException)
        {
            FirstPushAfterReady = null;
        }
    }

    public async Task DisposeAsync()
    {
        Http?.Dispose();

        if (_keycloak is not null)
        {
            await _keycloak.DisposeAsync();
        }

        if (Receiver is not null)
        {
            await Receiver.DisposeAsync();
        }
    }

    /// <summary>Keycloak's own log so far — the provider's lines among it.</summary>
    public async Task<string> LogsAsync()
    {
        var (stdout, stderr) = await _keycloak!.GetLogsAsync();

        return stdout + stderr;
    }

    /// <summary>An admin token of the master realm.</summary>
    public async Task<string> AdminTokenAsync()
    {
        using var response = await Http.PostAsync("/realms/master/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "admin-cli",
            ["grant_type"] = "password",
            ["username"] = "admin",
            ["password"] = _adminPassword,
        }));
        response.EnsureSuccessStatusCode();

        return (string)(await response.Content.ReadFromJsonAsync<JsonObject>())!["access_token"]!;
    }

    /// <summary>An access token of a user of realm <c>mustask</c> (direct grant of the realm's own <c>admin-cli</c> client).</summary>
    public async Task<string> UserTokenAsync(string username, string password)
    {
        using var response = await Http.PostAsync("/realms/mustask/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "admin-cli",
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password,
        }));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"the token request of {username} answered {(int)response.StatusCode}: {body}");

        return (string)JsonNode.Parse(body)!["access_token"]!;
    }

    /// <summary>
    /// A user updating their own profile through the account REST API, with the JSON text sent exactly as given —
    /// so it can carry what a .NET string cannot, such as an unpaired surrogate escape. Answers the status and the body.
    /// </summary>
    public async Task<(int Status, string Body)> UpdateOwnProfileAsync(string accessToken, string rawJson)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/realms/mustask/account")
        {
            Content = new StringContent(rawJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await Http.SendAsync(request);

        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Sends an admin REST request; answers the status and the body.</summary>
    public async Task<(int Status, string Body, Uri? Location)> AdminAsync(HttpMethod method, string path, JsonNode? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AdminTokenAsync());

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await Http.SendAsync(request);

        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(), response.Headers.Location);
    }

    /// <summary>Creates a user in realm <c>realm</c> and answers its id; the elapsed time of the call too.</summary>
    public async Task<(string Id, TimeSpan Elapsed)> CreateUserAsync(JsonObject user, string realm = "mustask")
    {
        var stopwatch = Stopwatch.StartNew();
        var (status, body, location) = await AdminAsync(HttpMethod.Post, $"/admin/realms/{realm}/users", user);
        stopwatch.Stop();

        Assert.True(status == 201, $"creating a user answered {status}: {body}");

        return (location!.Segments[^1], stopwatch.Elapsed);
    }

    private static void BuildImage()
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Mustask.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Mustask.sln not found above the test binaries.");
    }

    /// <summary>The live tests' own realm: the provider enabled; username editing on (D-15: here, not in the stand's import).</summary>
    private static string RealmImport() =>
        $$"""
        {
          "realm": "mustask",
          "enabled": true,
          "editUsernameAllowed": true,
          "eventsListeners": ["jboss-logging", "mustask-user-projection"],
          "clients": [
            {
              "clientId": "{{ServiceAccountClientId}}",
              "enabled": true,
              "publicClient": false,
              "secret": "{{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}}",
              "serviceAccountsEnabled": true,
              "standardFlowEnabled": false
            }
          ]
        }
        """;
}
