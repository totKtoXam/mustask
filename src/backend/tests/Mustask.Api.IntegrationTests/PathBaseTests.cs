using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Mustask.Api.Features.Session;
using Mustask.Api.Hosting;
using Mustask.Api.IntegrationTests.Features.Session;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// ADR-0037 (tech-057): <c>Hosting:PathBase</c>. Like <see cref="KeycloakOptionsTests"/>, the keys are process-wide
/// environment variables (the options are read before <c>Build()</c>), so the class is serial.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class PathBaseTests
{
    private const string PathBase = "/mustask";
    private const string PanelUrlUnderPathBase = "http://panel.invalid/mustask";

    private readonly MustaskApiFactory _factory;

    public PathBaseTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task WithEnvironmentAsync((string Name, string? Value)[] variables, Func<Task> body)
    {
        var previous = variables.Select(v => Environment.GetEnvironmentVariable(v.Name)).ToArray();

        try
        {
            foreach (var (name, value) in variables)
            {
                Environment.SetEnvironmentVariable(name, value);
            }

            await body();
        }
        finally
        {
            for (var i = 0; i < variables.Length; i++)
            {
                Environment.SetEnvironmentVariable(variables[i].Name, previous[i]);
            }
        }
    }

    [Theory]
    [InlineData("mustask")]
    [InlineData("/mustask/")]
    [InlineData("/")]
    [InlineData("//mustask")]
    [InlineData("/must/ask/")]
    [InlineData("/mustask?x=1")]
    [InlineData("/mustask#top")]
    [InlineData("/must%61sk")]
    [InlineData("/must\\ask")]
    [InlineData("/../mustask")]
    [InlineData("http://panel.invalid/mustask")]
    public void ParsePathBase_Malformed_Throws(string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => SessionOidcOptions.ParsePathBase(value));
        Assert.Contains("Hosting:PathBase", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    [InlineData("/mustask", "/mustask")]
    [InlineData("/apps/must-ask_1.0", "/apps/must-ask_1.0")]
    public void ParsePathBase_WellFormedOrEmpty_IsAccepted(string? value, string expected)
    {
        Assert.Equal(expected, SessionOidcOptions.ParsePathBase(value));
    }

    [Fact]
    public async Task MalformedPathBase_HostDoesNotStart()
    {
        await WithEnvironmentAsync([("Hosting__PathBase", "mustask/")], () =>
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
            Assert.Contains("Hosting:PathBase", exception.Message, StringComparison.Ordinal);
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData("http://panel.invalid")]
    [InlineData("http://panel.invalid/other")]
    [InlineData("http://panel.invalid/mustask/deeper")]
    [InlineData("http://panel.invalid/MUSTASK")]
    public async Task PathBaseNotMatchingPanelUrlPath_HostDoesNotStart(string panelUrl)
    {
        await WithEnvironmentAsync([("Hosting__PathBase", PathBase), ("Session__PanelUrl", panelUrl)], () =>
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
            Assert.Contains("Session:PanelUrl", exception.Message, StringComparison.Ordinal);
            Assert.Contains("Hosting:PathBase", exception.Message, StringComparison.Ordinal);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EmptyPathBaseWithPanelUrlPath_HostDoesNotStart()
    {
        // A forgotten Hosting__PathBase: refused at start, not at the first sign-in (ADR-0037).
        await WithEnvironmentAsync([("Hosting__PathBase", ""), ("Session__PanelUrl", "http://panel.invalid/mustask")], () =>
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
            Assert.Contains("Session:PanelUrl", exception.Message, StringComparison.Ordinal);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EmptyPathBase_RootBehaviourUnchanged()
    {
        // The factory's default: no Hosting:PathBase, PanelUrl without a path.
        var client = _factory.CreateNoRedirectClient();

        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");

        var redirectUri = QueryHelpers.ParseQuery(challenge.AuthorizationLocation.Query)["redirect_uri"].ToString();
        Assert.Equal("http://localhost/api/session/callback", redirectUri);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{PathBase}/health")).StatusCode);
    }

    [Fact]
    public async Task UnderPathBase_RedirectUriCookiesAndRedirectsCarryThePrefix()
    {
        await WithEnvironmentAsync([("Hosting__PathBase", PathBase), ("Session__PanelUrl", PanelUrlUnderPathBase)], async () =>
        {
            using var factory = _factory.WithWebHostBuilder(_ => { });
            var client = factory.CreateNoRedirectClient();

            // /health works both through the prefix (the proxy passes it unchanged) and without it (the VM itself).
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{PathBase}/health")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);

            using var login = await client.GetAsync($"{PathBase}/api/session/login?returnUrl={Uri.EscapeDataString("/dashboard")}");
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            var query = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
            Assert.Equal("http://localhost/mustask/api/session/callback", query["redirect_uri"].ToString());

            // The correlation and nonce cookies are scoped to the callback path, prefix included.
            var challengeCookies = login.Headers.GetValues("Set-Cookie").ToList();
            Assert.NotEmpty(challengeCookies);
            Assert.All(challengeCookies, header =>
                Assert.Contains("path=/mustask/api/session/callback", header, StringComparison.OrdinalIgnoreCase));

            var sid = Guid.CreateVersion7().ToString();
            _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
                accessToken: KeycloakTestRealm.CreateToken(["dictionaries.languages.read"]),
                idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, query["nonce"].ToString()),
                refreshToken: "test-refresh-token-pathbase",
                expiresIn: 300,
                refreshExpiresIn: 1800);

            try
            {
                using var callback = new HttpRequestMessage(HttpMethod.Post, $"{PathBase}/api/session/callback")
                {
                    Content = new FormUrlEncodedContent(
                    [
                        new KeyValuePair<string, string>("code", "test-code"),
                        new KeyValuePair<string, string>("state", query["state"].ToString()),
                    ]),
                };
                SessionTestSupport.AttachCookies(callback, SessionTestSupport.CapturedCookies(login));

                using var response = await client.SendAsync(callback);

                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.Equal("http://panel.invalid/mustask/dashboard", response.Headers.Location!.ToString());

                var sessionCookie = SessionTestSupport.SessionCookie(response);
                Assert.NotNull(sessionCookie);
                Assert.Contains("path=/mustask/api/session", sessionCookie, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                _factory.TokenResponder.Reset();
            }
        });
    }

    [Fact]
    public async Task RelativeLocation_GetsThePathBaseOnlyWhenTheRequestCarriedIt()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UsePathBase(PathBase);
        app.UsePathBaseOnRelativeLocation();
        app.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status201Created;
            context.Response.Headers.Location = "/api/things/1";
            return Task.CompletedTask;
        });
        await app.StartAsync();
        var client = app.GetTestClient();

        using var prefixed = await client.GetAsync($"{PathBase}/anything");
        using var bare = await client.GetAsync("/anything");

        Assert.Equal("/mustask/api/things/1", prefixed.Headers.Location!.ToString());
        Assert.Equal("/api/things/1", bare.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("https://portal.invalid/mustask/dashboard")]
    [InlineData("//other.invalid/x")]
    [InlineData("relative/no-slash")]
    [InlineData(null)]
    public async Task Location_NotRootRelative_IsLeftAlone(string? location)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UsePathBase(PathBase);
        app.UsePathBaseOnRelativeLocation();
        app.Run(context =>
        {
            if (location is not null)
            {
                context.Response.Headers.Location = location;
            }

            return Task.CompletedTask;
        });
        await app.StartAsync();
        var client = app.GetTestClient();

        using var response = await client.GetAsync($"{PathBase}/anything");

        if (location is null)
        {
            Assert.False(response.Headers.Contains("Location"));
        }
        else
        {
            Assert.Equal(location, response.Headers.GetValues("Location").Single());
        }
    }

    [Fact]
    public async Task UnderPathBase_ApiRouteThroughThePrefix_IsRoutedAndChallenged()
    {
        // The real pipeline (not the stand-in above) routes an /api request through the prefix; no token, so 401.
        await WithEnvironmentAsync([("Hosting__PathBase", PathBase), ("Session__PanelUrl", PanelUrlUnderPathBase)], async () =>
        {
            using var factory = _factory.WithWebHostBuilder(_ => { });
            var client = factory.CreateNoRedirectClient();

            using var response = await client.GetAsync($"{PathBase}/api/dictionaries/languages");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await Task.CompletedTask;
        });
    }
}
