using System.Net.Http.Json;
using System.Text.Json;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// <c>ICurrentUser.DisplayName</c> — claim
/// <c>name</c>, при его отсутствии <c>preferred_username</c>, при
/// отсутствии обоих или анонимном запросе — <c>"anonymous"</c>. Никакого
/// фолбэка на <c>sub</c>: это UUID, а не имя. Проверено через
/// тестовые endpoint'ы <c>/__test/current-user</c> (<c>AuthenticatedOnly</c>)
/// и <c>/__test/current-user-public</c> (<c>AllowAnonymous</c>),
/// <see cref="TestEndpointsStartupFilter"/>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CurrentUserTests
{
    private readonly MustaskApiFactory _factory;

    public CurrentUserTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> DisplayNameFromAsync(HttpRequestMessage request)
    {
        var response = await _factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("displayName").GetString()!;
    }

    [Fact]
    public async Task NameClaimPresent_UsesName()
    {
        var token = KeycloakTestRealm.CreateToken(configure: t =>
        {
            t.Name = "Иван Инспекторов";
            t.PreferredUsername = "member";
        });

        var displayName = await DisplayNameFromAsync(
            new HttpRequestMessage(HttpMethod.Get, "/__test/current-user").WithBearer(token));

        Assert.Equal("Иван Инспекторов", displayName);
    }

    [Fact]
    public async Task NameClaimAbsent_FallsBackToPreferredUsername()
    {
        var token = KeycloakTestRealm.CreateToken(configure: t =>
        {
            t.Name = null;
            t.PreferredUsername = "member";
        });

        var displayName = await DisplayNameFromAsync(
            new HttpRequestMessage(HttpMethod.Get, "/__test/current-user").WithBearer(token));

        Assert.Equal("member", displayName);
    }

    /// <summary>Без name/preferred_username DisplayName — константа "anonymous", а не "sub" (UUID).</summary>
    [Fact]
    public async Task NeitherClaimPresent_ReturnsAnonymous_NotSub()
    {
        var subject = Guid.CreateVersion7().ToString();
        var token = KeycloakTestRealm.CreateToken(configure: t =>
        {
            t.Name = null;
            t.PreferredUsername = string.Empty;
            t.Subject = subject;
        });

        var displayName = await DisplayNameFromAsync(
            new HttpRequestMessage(HttpMethod.Get, "/__test/current-user").WithBearer(token));

        Assert.Equal("anonymous", displayName);
        Assert.NotEqual(subject, displayName);
    }

    [Fact]
    public async Task PublicRouteWithoutToken_ReturnsAnonymous()
    {
        var displayName = await DisplayNameFromAsync(
            new HttpRequestMessage(HttpMethod.Get, "/__test/current-user-public"));

        Assert.Equal("anonymous", displayName);
    }
}
