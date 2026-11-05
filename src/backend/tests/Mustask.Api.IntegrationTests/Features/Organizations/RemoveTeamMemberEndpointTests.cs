using System.Net;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/teams/{teamId}/members/{userId}</c> (mod-016 api-contract
/// § 2.4; US-3 "An administrator removes a team member", "Removing a team member who is already gone"): the user leaves this team
/// only — they stay in the organization with their role and still read every team.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RemoveTeamMemberEndpointTests(MustaskApiFactory factory)
{
    private const string MemberNotFound = "organizations.team.member_not_found";

    private Task<Answer> RemoveAsync(OrganizationStructure structure, Guid teamId, string userId, string? token) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.OrgTeams(structure.OrganizationId)}/{teamId}/members/{Uri.EscapeDataString(userId)}",
            token: token);

    private async Task<(OrganizationStructure Structure, string AdminToken)> WorldAsync()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory, founded: false);
        var admin = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);

        return (structure, admin.Token);
    }

    // US-3 · "An administrator removes a team member": 204; the user leaves this team and stays in the other; they stay in the
    // organization with their role, and the team's list no longer shows them.
    [Fact]
    public async Task Remove_ATeamMember_Returns204_TheyStayInTheOrganization_AndInOtherTeams()
    {
        var (structure, token) = await WorldAsync();
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var staying = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var teamId = structure.Platform.Id;
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, member.Id);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, staying.Id);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.ProjectTeamId, member.Id);

        var answer = await RemoveAsync(structure, teamId, member.Id, token);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal([staying.Id], await MembershipTestData.TeamMemberIdsAsync(factory, teamId));
        Assert.Equal([member.Id], await MembershipTestData.TeamMemberIdsAsync(factory, structure.ProjectTeamId));
        Assert.Equal(OrganizationRole.Member, (await MembershipTestData.FindMembershipAsync(factory, structure.OrganizationId, member.Id))!.Role);
    }

    // US-3 · "the user still reads every team": after the removal the member's own token still reads this team's members and the
    // organization's access.
    [Fact]
    public async Task Remove_TheRemovedUserStillReadsEveryTeam()
    {
        var (structure, token) = await WorldAsync();
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var teamId = structure.Platform.Id;
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, member.Id);

        await RemoveAsync(structure, teamId, member.Id, token);
        var members = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"{OrganizationsApi.OrgTeams(structure.OrganizationId)}/{teamId}/members", token: member.Token);
        var access = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{OrganizationsApi.Organization(structure.OrganizationId)}/access", token: member.Token);

        Assert.Equal(HttpStatusCode.OK, members.Status);
        Assert.Equal(HttpStatusCode.OK, access.Status);
        Assert.Equal("member", access.Body.GetProperty("role").GetString());
    }

    // US-3 · "Removing a team member who is already gone": 404 «Участник команды не найден.» — removed twice, a member of the
    // organization who is not in this team (in another team only), a user of no membership, an over-long id.
    [Fact]
    public async Task Remove_AMemberAlreadyGoneOrNotInThisTeam_Returns404TeamMemberNotFound()
    {
        var (structure, token) = await WorldAsync();
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var elsewhere = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var teamId = structure.Platform.Id;
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, member.Id);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.ProjectTeamId, elsewhere.Id);

        var first = await RemoveAsync(structure, teamId, member.Id, token);
        var second = await RemoveAsync(structure, teamId, member.Id, token);
        var inAnotherTeam = await RemoveAsync(structure, teamId, elsewhere.Id, token);
        var stranger = await RemoveAsync(structure, teamId, MembershipTestData.NewUserId(), token);
        var tooLong = await RemoveAsync(structure, teamId, new string('u', 256), token);

        Assert.Equal(HttpStatusCode.NoContent, first.Status);
        foreach (var answer in new[] { second, inAnotherTeam, stranger, tooLong })
        {
            Assert.Equal((HttpStatusCode.NotFound, MemberNotFound), (answer.Status, answer.ErrorCode));
            Assert.Equal("Участник команды не найден.", answer.Detail);
        }

        Assert.Equal([elsewhere.Id], await MembershipTestData.TeamMemberIdsAsync(factory, structure.ProjectTeamId));
    }

    // feat-028 api-contract § 2.3 · the team must be an active team of THIS organization: an unknown, malformed or another organization's team answers
    // `team.not_found` — and nothing is removed (the project left the address).
    [Fact]
    public async Task Remove_FromTheWrongContainerOrADeletedTeam_AnswersTheLevelNotFound()
    {
        var (structure, token) = await WorldAsync();
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.Platform.Id, member.Id);
        var elsewhere = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = OrganizationsApi.Organization(structure.OrganizationId);

        var wrongTeams = new[]
        {
            $"{organization}/teams/{Guid.CreateVersion7()}/members/{member.Id}",
            $"{organization}/teams/abc/members/{member.Id}",
            $"{organization}/teams/{elsewhere.Platform.Id}/members/{member.Id}",
        };

        foreach (var path in wrongTeams)
        {
            var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Delete, path, token: token);

            Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
        }

        Assert.Equal([member.Id], await MembershipTestData.TeamMemberIdsAsync(factory, structure.Platform.Id));
    }

    // US-2 · "A member whose role lacks the code is refused": a member and a viewer get 403; nothing is removed.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Remove_MemberAndViewer_Get403_NothingIsRemoved(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, role);
        var teamId = structure.Platform.Id;
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, caller.Id);

        var self = await RemoveAsync(structure, teamId, caller.Id, caller.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (self.Status, self.ErrorCode));
        Assert.Equal([caller.Id], await MembershipTestData.TeamMemberIdsAsync(factory, teamId));
    }

    // US-2 · "A non-member gets «Организация не найдена»" and 401 without a token.
    [Fact]
    public async Task Remove_NonMember_Is404_NoToken_Is401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var teamId = structure.Platform.Id;
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, member.Id);

        var nonMember = await RemoveAsync(structure, teamId, member.Id, MembershipTestData.Outsider());
        var anonymous = await RemoveAsync(structure, teamId, member.Id, token: null);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (nonMember.Status, nonMember.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.Equal([member.Id], await MembershipTestData.TeamMemberIdsAsync(factory, teamId));
    }
}
