using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.UserDirectory;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/workitems/assignee-candidates?text=</c>
/// (mod-003 api-contract § 2.5, US-4, BR-M3-22): every active user of the
/// directory, by display name or e-mail. Each test empties the projection
/// first (<see cref="UserDirectoryTestData.ResetAsync"/>), as mod-014's own
/// read tests do: the collection shares one database.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetAssigneeCandidatesEndpointTests(MustaskApiFactory factory)
{
    /// <summary>US-4's directory: «Анна Смирнова» and «Анна Кузнецова» active, «Пётр Иванов» inactive.</summary>
    private async Task<(Guid OrganizationId, string Smirnova, string Kuznetsova, string Ivanov)> DirectoryAsync()
    {
        await UserDirectoryTestData.ResetAsync(factory);
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var smirnova = await WorkItemsTestData.AddUserAsync(factory, "Анна", "Смирнова", "anna.smirnova@example.com");
        var kuznetsova = await WorkItemsTestData.AddUserAsync(factory, "Анна", "Кузнецова", "anna.kuznetsova@example.com");
        var ivanov = await WorkItemsTestData.AddUserAsync(factory, "Пётр", "Иванов", "petr.ivanov@example.com", active: false);

        return (organization.Id, smirnova, kuznetsova, ivanov);
    }

    private Task<Answer> SearchAsync(Guid organizationId, string? text) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, WorkItemsTestData.Candidates(organizationId, text), token: OrganizationsTokens.SignedIn());

    private static List<(string? Id, string? Name, string? Email)> Users(JsonElement body) =>
        body.GetProperty("users").EnumerateArray()
            .Select(user => (user.GetProperty("id").GetString(), user.GetProperty("displayName").GetString(), user.GetProperty("email").GetString()))
            .ToList();

    // US-4 · "An assignee is found by name": «анна» offers both Annas, each with her e-mail, in display order.
    [Fact]
    public async Task Search_ByName_OffersTheMatchingActiveUsers_WithEmail()
    {
        var directory = await DirectoryAsync();

        var answer = await SearchAsync(directory.OrganizationId, "анна");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(
            [(directory.Kuznetsova, "Анна Кузнецова", "anna.kuznetsova@example.com"), (directory.Smirnova, "Анна Смирнова", "anna.smirnova@example.com")],
            Users(answer.Body));
        Assert.False(answer.Body.GetProperty("moreMatched").GetBoolean());
    }

    // US-4 · "A user is found by e-mail".
    [Fact]
    public async Task Search_ByEmail_OffersTheUser()
    {
        var directory = await DirectoryAsync();

        var answer = await SearchAsync(directory.OrganizationId, "smirnova@");

        Assert.Equal([directory.Smirnova], Users(answer.Body).Select(user => user.Id));
    }

    // US-4 · "The picker's hints": one letter and no text answer nobody; nobody matches «Ёжиков».
    [Theory]
    [InlineData("а")]
    [InlineData("Ёжиков")]
    [InlineData(null)]
    public async Task Search_ShortTextOrNoMatch_OffersNobody(string? text)
    {
        var directory = await DirectoryAsync();

        var answer = await SearchAsync(directory.OrganizationId, text);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(Users(answer.Body));
        Assert.False(answer.Body.GetProperty("moreMatched").GetBoolean());
    }

    // US-4 · "The picker's hints": 25 active users match, 20 are offered and more matched.
    [Fact]
    public async Task Search_25Match_Offers20_MoreMatched()
    {
        var directory = await DirectoryAsync();
        for (var index = 1; index <= 25; index++)
        {
            await WorkItemsTestData.AddUserAsync(factory, "Мария", $"Тестова{index:00}");
        }

        var answer = await SearchAsync(directory.OrganizationId, "мария");

        Assert.Equal(20, Users(answer.Body).Count);
        Assert.True(answer.Body.GetProperty("moreMatched").GetBoolean());
    }

    // US-4 · "An inactive user cannot be newly assigned": «Пётр Иванов» is never offered.
    [Fact]
    public async Task Search_InactiveUser_IsNotOffered()
    {
        var directory = await DirectoryAsync();

        Assert.Empty(Users((await SearchAsync(directory.OrganizationId, "Пётр")).Body));
        Assert.Empty(Users((await SearchAsync(directory.OrganizationId, "petr.ivanov")).Body));
    }

    // api-contract § 2.5: a text over 100 characters is mod-014's refusal, passed through.
    [Fact]
    public async Task Search_TextOver100_Returns400_UserDirectoryValidationFailed()
    {
        var directory = await DirectoryAsync();

        var answer = await SearchAsync(directory.OrganizationId, new string('а', 101));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("user_directory.validation_failed", answer.ErrorCode);
        Assert.Equal("user_directory.value_too_long", answer.Body.GetProperty("errors").GetProperty("text")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Search_NoToken_Returns401()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, WorkItemsTestData.Candidates(organization.Id, "анна"));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }
}
