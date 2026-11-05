using System.Net;
using System.Text.Json;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/teams/{teamId}/description</c> (feat-028 api-contract § 0.2, § 2.1; US-1 "rename and
/// description") through the real host and a real PostgreSQL: the description is kept exactly as typed, the empty string, <c>null</c>
/// and an absent field clear it, and 2 001 characters are refused. Every test builds its own organization.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SetTeamDescriptionEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> DescribeAsync(Guid organizationId, string teamId, object? body, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.OrgTeams(organizationId)}/{Uri.EscapeDataString(teamId)}/description",
            body,
            token ?? OrganizationsTokens.SignedIn());

    private async Task<string?> StoredAsync(Guid teamId) => (await StoredTeamAsync(factory, teamId)).Description;

    // feat-028 US-1 · "Describe_KeptAsTyped": spaces and line breaks inside and around the text stay, the answer is the `Team` of
    // api-contract § 1.1 and a read of the team returns the same text.
    [Fact]
    public async Task Describe_KeptAsTyped_AnswersTheTeam()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var typed = "  Платформа и\nинструменты  ";

        var answer = await DescribeAsync(structure.OrganizationId, structure.Platform.Id.ToString(), new { description = typed });
        var read = await GetAsync(factory, Team(structure.OrganizationId, structure.Platform.Id));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(["description", "id", "name"], PropertyNames(answer.Body));
        Assert.Equal(structure.Platform.Id, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal("Платформа", answer.Body.GetProperty("name").GetString());
        Assert.Equal(typed, answer.Body.GetProperty("description").GetString());
        Assert.Equal(typed, read.Body.GetProperty("description").GetString());
        Assert.Equal(typed, await StoredAsync(structure.Platform.Id));
    }

    // feat-028 § 0.2 · "Describe_ClearedByEmptyNullAndAbsent": the empty string, null, `{}` and no body all clear it, and the answer
    // says null.
    [Theory]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("absent")]
    [InlineData("nobody")]
    public async Task Describe_ClearedByEmptyNullAbsentAndNoBody(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await DescribeAsync(structure.OrganizationId, structure.Platform.Id.ToString(), new { description = "Было" });
        object? body = how switch
        {
            "empty" => new { description = "" },
            "null" => new { description = (string?)null },
            "absent" => new { },
            _ => null,
        };

        var answer = await DescribeAsync(structure.OrganizationId, structure.Platform.Id.ToString(), body);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("description").ValueKind);
        Assert.Null(await StoredAsync(structure.Platform.Id));
    }

    // feat-028 § 0.2 · "Describe_2001Characters_Returns400_Unchanged": 2 000 passes, 2 001 is refused with the field reason under
    // `description`, and the stored text is the one before.
    [Fact]
    public async Task Describe_2001Characters_Returns400_Unchanged_2000Passes()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await DescribeAsync(structure.OrganizationId, structure.Platform.Id.ToString(), new { description = "Было" });

        var refused = await DescribeAsync(structure.OrganizationId, structure.Platform.Id.ToString(), new { description = new string('я', 2001) });
        Assert.Equal("Было", await StoredAsync(structure.Platform.Id));
        var longest = await DescribeAsync(structure.OrganizationId, structure.Platform.Id.ToString(), new { description = new string('я', 2000) });

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Equal("organizations.validation_failed", refused.ErrorCode);
        Assert.Equal("organizations.description_length", ReasonOf(refused, "description"));
        Assert.Equal(HttpStatusCode.OK, longest.Status);
    }

    // feat-028 BR-OL-3 · the description changes the description and nothing else: not the name, the links, the members.
    [Fact]
    public async Task Describe_ChangesOnlyTheDescription()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var linksBefore = (await LinksOfTeamAsync(factory, structure.Platform.Id)).Select(link => (link.ProjectId, link.LinkedBy, link.LinkedAt)).ToList();
        var membersBefore = await TeamMemberIdsAsync(factory, structure.Platform.Id);

        await DescribeAsync(structure.OrganizationId, structure.Platform.Id.ToString(), new { description = "Новое" });

        Assert.Equal("Платформа", (await StoredTeamAsync(factory, structure.Platform.Id)).Name);
        Assert.Equal(linksBefore, (await LinksOfTeamAsync(factory, structure.Platform.Id)).Select(link => (link.ProjectId, link.LinkedBy, link.LinkedAt)).ToList());
        Assert.Equal(membersBefore, await TeamMemberIdsAsync(factory, structure.Platform.Id));
    }

    // feat-028 US-1 · roles: organizations.team.write is the administrator's alone; the 403 comes before the team and the body.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Describe_MemberAndViewer_Return403_ChangeNothing(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, role);

        var answer = await DescribeAsync(structure.OrganizationId, Guid.CreateVersion7().ToString(), new { description = new string('я', 2001) }, caller.Token);
        var real = await DescribeAsync(structure.OrganizationId, structure.Platform.Id.ToString(), new { description = "Новое" }, caller.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (answer.Status, answer.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (real.Status, real.ErrorCode));
        Assert.Null(await StoredAsync(structure.Platform.Id));
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Describe_NoToken_Returns401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{Team(structure.OrganizationId, structure.Platform.Id)}/description", new { description = "Новое" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Null(await StoredAsync(structure.Platform.Id));
    }

    // api-contract § 0.4 · a team that is not an active team of the organization — malformed, unknown, another organization's or
    // deleted — answers team.not_found before the body is looked at (the body is too long too), and changes nothing.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deleted")]
    public async Task Describe_TeamNotAnActiveTeamOfTheOrganization_Returns404TeamNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var core = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");

        if (how == "deleted")
        {
            await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, core.Id);
        }

        var teamId = how switch
        {
            "malformed" => "not-a-team",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.Platform.Id.ToString(),
            _ => core.Id.ToString(),
        };

        var answer = await DescribeAsync(structure.OrganizationId, teamId, new { description = new string('я', 2001) });

        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
        Assert.Null(await StoredAsync(other.Platform.Id));
    }
}
