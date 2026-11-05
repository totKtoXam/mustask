using System.Net;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/rescue/appointee-candidates</c> (feat-033 api-contract § 1.2; US-2 "A system administrator finds the
/// person"): the projection's people by name or e-mail, with no organization in the request (backend D-4).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SearchAppointeeCandidatesEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> SearchAsync(string text, string? token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{RescueTestData.Rescue}/appointee-candidates?text={Uri.EscapeDataString(text)}", token: token);

    // US-2: found by a part of the name and by the e-mail; the fields are exactly id, displayName and email — the same for a member
    // of some organization and for a person who belongs to none.
    [Fact]
    public async Task Search_FindsByNameAndByEmail_WithOnlyThreeFields()
    {
        var tag = Guid.CreateVersion7().ToString("N");
        var (_, token) = RescueTestData.NewRescuer();
        var free = await RescueTestData.SignedInUserAsync(factory, $"Свободная{tag}", "Иванова", $"free.{tag}@example.org");
        var member = await RescueTestData.SignedInUserAsync(factory, $"Занятая{tag}", "Петрова", $"busy.{tag}@example.org");
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        await MembershipTestData.AddMemberAsync(factory, organization.Id, member, OrganizationRole.Member);

        var byName = await SearchAsync($"Свободная{tag}", token);
        var byEmail = await SearchAsync($"busy.{tag}", token);

        Assert.Equal(HttpStatusCode.OK, byName.Status);
        var found = Assert.Single(byName.Body.GetProperty("users").EnumerateArray());
        Assert.Equal(free, found.GetProperty("id").GetString());
        Assert.Equal(["displayName", "email", "id"], found.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        var other = Assert.Single(byEmail.Body.GetProperty("users").EnumerateArray());
        Assert.Equal(["displayName", "email", "id"], other.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(member, other.GetProperty("id").GetString());
        Assert.False(byName.Body.GetProperty("moreMatched").GetBoolean());
    }

    [Fact]
    public async Task Search_AnUnknownPerson_AndAnInactiveRow_AreNotFound()
    {
        var tag = Guid.CreateVersion7().ToString("N");
        var (_, token) = RescueTestData.NewRescuer();
        await MembershipTestData.ProjectUserAsync(factory, MembershipTestData.NewUserId(), $"Старый{tag}", "Неактивный", active: false);

        Assert.Empty((await SearchAsync($"Старый{tag}", token)).Body.GetProperty("users").EnumerateArray());
        Assert.Empty((await SearchAsync($"Никого{tag}", token)).Body.GetProperty("users").EnumerateArray());
    }

    [Fact]
    public async Task Search_OneCharacter_IsEmpty_AndOver100Characters_Returns400()
    {
        var (_, token) = RescueTestData.NewRescuer();

        var short1 = await SearchAsync("а", token);
        var tooLong = await SearchAsync(new string('а', 101), token);

        Assert.Equal(HttpStatusCode.OK, short1.Status);
        Assert.Empty(short1.Body.GetProperty("users").EnumerateArray());
        Assert.Equal((HttpStatusCode.BadRequest, "user_directory.validation_failed"), (tooLong.Status, tooLong.ErrorCode));
    }

    [Fact]
    public async Task Search_WithoutTheRight_Returns403_AndAnonymous401()
    {
        foreach (var token in new[] { OrganizationsTokens.AllInstanceRolesButSystemAdmin(), OrganizationsTokens.SignedIn() })
        {
            var answer = await SearchAsync("иван", token);

            Assert.Equal((HttpStatusCode.Forbidden, OrganizationErrorCodes.RescueForbidden), (answer.Status, answer.ErrorCode));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await SearchAsync("иван", null)).Status);
    }
}
