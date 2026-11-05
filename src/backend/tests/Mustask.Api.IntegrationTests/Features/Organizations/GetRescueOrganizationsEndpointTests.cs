using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/rescue</c> (feat-033 api-contract § 1.1; US-1 "The system administrator finds an organization"):
/// every organization by name, 25 per page, with the true count, the status and the last appointment. The shard database is shared,
/// so each test names its organizations after a unique tag and filters by it.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetRescueOrganizationsEndpointTests(MustaskApiFactory factory)
{
    private static string Tag() => Guid.CreateVersion7().ToString("N");

    private Task<Answer> ListAsync(string query, string? token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{RescueTestData.Rescue}{query}", token: token);

    private static string[] Names(Answer answer) =>
        answer.Body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("name").GetString()!).ToArray();

    // US-1: by name, with status and the last appointment (null when none); the row has exactly these four fields.
    [Fact]
    public async Task List_ReturnsRowsByName_WithStatusAndLastAppointment()
    {
        var tag = Tag();
        var (_, token) = RescueTestData.NewRescuer();
        var beta = await RescueTestData.OrphanedOrganizationAsync(factory, $"Бета {tag}", OrganizationStatus.Deactivated);
        var alpha = await RescueTestData.OrphanedOrganizationAsync(factory, $"Альфа {tag}");
        var user = await RescueTestData.SignedInUserAsync(factory, "Кто", "Назначен");
        await OrganizationsApi.SendAsync(factory, HttpMethod.Post, RescueTestData.AppointmentsPath(alpha.Id), new { userId = user, reason = "Причина" }, token);

        var answer = await ListAsync($"?name={tag}", token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(2, answer.Body.GetProperty("count").GetInt32());
        Assert.Equal([$"Альфа {tag}", $"Бета {tag}"], Names(answer));
        var rows = answer.Body.GetProperty("data").EnumerateArray().ToArray();
        Assert.Equal(["id", "lastAppointedAt", "name", "status"], rows[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(("active", alpha.Id), (rows[0].GetProperty("status").GetString(), rows[0].GetProperty("id").GetGuid()));
        Assert.NotEqual(JsonValueKind.Null, rows[0].GetProperty("lastAppointedAt").ValueKind);
        Assert.Equal(("deactivated", beta.Id), (rows[1].GetProperty("status").GetString(), rows[1].GetProperty("id").GetGuid()));
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("lastAppointedAt").ValueKind);
    }

    // US-1 filter table: a part of the name, any case, ё = е, % and _ are literal. The tag in front keeps the shared database out.
    [Theory]
    [InlineData("Гамма", "гам", true)]
    [InlineData("Гамма", "ГАМ", true)]
    [InlineData("Ёлка", "елка", true)]
    [InlineData("Елка", "ёлка", true)]
    [InlineData("100% скидка", "100%", true)]
    [InlineData("1000 скидка", "100%", false)]
    [InlineData("a_b имя", "a_b", true)]
    [InlineData("axb имя", "a_b", false)]
    [InlineData("Гамма", "нет такой", false)]
    public async Task List_FiltersByAPartOfTheName(string organizationName, string filter, bool found)
    {
        var tag = Tag();
        var (_, token) = RescueTestData.NewRescuer();
        await RescueTestData.OrphanedOrganizationAsync(factory, $"{tag} {organizationName}");

        var answer = await ListAsync($"?name={Uri.EscapeDataString($"{tag} {filter}")}", token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(found ? [$"{tag} {organizationName}"] : [], Names(answer));
        Assert.Equal(found ? 1 : 0, answer.Body.GetProperty("count").GetInt32());
    }

    // US-1: spaces around the filter are ignored; a blank filter lists everything (the tag is not needed to see the count grow).
    [Fact]
    public async Task List_SurroundingSpacesAreIgnored()
    {
        var tag = Tag();
        var (_, token) = RescueTestData.NewRescuer();
        await RescueTestData.OrphanedOrganizationAsync(factory, $"{tag} Бета");

        var answer = await ListAsync($"?name={Uri.EscapeDataString($"   {tag} бет  ")}", token);

        Assert.Equal([$"{tag} Бета"], Names(answer));
    }

    // US-1 "Many organizations": 25 a page, the true count, the second page is the rest, a page past the end is empty with the count.
    [Fact]
    public async Task List_PagesBy25_WithTheTrueCount_AndAnEmptyPagePastTheEnd()
    {
        var tag = Tag();
        var (_, token) = RescueTestData.NewRescuer();

        for (var i = 0; i < 30; i++)
        {
            await RescueTestData.OrphanedOrganizationAsync(factory, $"Страница {i:D2} {tag}");
        }

        var first = await ListAsync($"?name={tag}", token);
        var second = await ListAsync($"?name={tag}&page=2", token);
        var past = await ListAsync($"?name={tag}&page=3", token);

        Assert.Equal(30, first.Body.GetProperty("count").GetInt32());
        Assert.Equal(25, Names(first).Length);
        Assert.Equal(5, Names(second).Length);
        Assert.Equal(Names(first).Concat(Names(second)).Order(StringComparer.Ordinal), Names(first).Concat(Names(second)));
        Assert.Empty(Names(past));
        Assert.Equal(30, past.Body.GetProperty("count").GetInt32());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    public async Task List_AnInvalidPage_Returns400(string page)
    {
        var (_, token) = RescueTestData.NewRescuer();

        var answer = await ListAsync($"?page={page}", token);

        Assert.Equal((HttpStatusCode.BadRequest, OrganizationErrorCodes.ValidationFailed), (answer.Status, answer.ErrorCode));
        Assert.Equal(
            OrganizationErrorCodes.RescueInvalidPage,
            answer.Body.GetProperty("errors").GetProperty("page")[0].GetProperty("code").GetString());
    }

    // Code review CR-3, contract § 1.1 "never an error": a filter holding U+0000 matches nothing (PostgreSQL refuses the character).
    [Fact]
    public async Task List_ANameWithANulCharacter_MatchesNothing_NeverAnError()
    {
        var (_, token) = RescueTestData.NewRescuer();

        var answer = await ListAsync("?name=%00", token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(0, answer.Body.GetProperty("count").GetInt32());
        Assert.Empty(Names(answer));
    }

    // Plan § 10: an empty page value is as invalid as a letter (the binding, not only the handler).
    [Fact]
    public async Task List_AnEmptyPage_Returns400()
    {
        var (_, token) = RescueTestData.NewRescuer();

        var answer = await ListAsync("?page=", token);

        Assert.Equal((HttpStatusCode.BadRequest, OrganizationErrorCodes.ValidationFailed), (answer.Status, answer.ErrorCode));
    }

    [Fact]
    public async Task List_WithoutTheRight_Returns403_AndAnonymous401()
    {
        foreach (var token in new[] { OrganizationsTokens.AllInstanceRolesButSystemAdmin(), OrganizationsTokens.SignedIn() })
        {
            var answer = await ListAsync(string.Empty, token);

            Assert.Equal((HttpStatusCode.Forbidden, OrganizationErrorCodes.RescueForbidden), (answer.Status, answer.ErrorCode));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await ListAsync(string.Empty, null)).Status);
    }
}
