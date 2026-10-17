using System.Net;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>PUT …/projects/{projectId}/default-team</c> (mod-017 api-contract §§ 0.2, 2.8; US-3, BR-OT-4): an administrator names the
/// team new items start on, or clears the setting. It decides a creation's starting value and nothing else. Every test builds its
/// own world (<see cref="OwningTeamTestData"/>): the database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SetDefaultTeamEndpointTests(MustaskApiFactory factory)
{
    private const string TeamNotFound = "organizations.team.not_found";
    private const string TeamNotInProject = "organizations.team.not_in_project";

    private string Path(OwningTeamWorld world) => $"{OrganizationsApi.Project(world.OrganizationId, world.ProjectId)}/default-team";

    private Task<Answer> NameAsync(OwningTeamWorld world, object? body, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Put, Path(world), body, token ?? OrganizationsTokens.SignedIn());

    private async Task<Guid?> SettingAsync(Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.DefaultTeamId)
            .SingleAsync();
    }

    private async Task<List<(Guid Id, string? Mark)>> MarksAsync(OwningTeamWorld world)
    {
        var teams = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, OrganizationsApi.Teams(world.OrganizationId, world.ProjectId), token: OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.OK, teams.Status);

        return teams.Body.EnumerateArray()
            .Select(team => (team.GetProperty("id").GetGuid(), team.GetProperty("defaultMark").GetString()))
            .ToList();
    }

    // US-3 · "the administrator chooses the default": naming a team answers 204 with no body; the teams read marks it «named»,
    // and no other team carries a mark.
    [Fact]
    public async Task Name_Team_MarksItInTheTeamsRead_OthersSeeTheMarks()
    {
        var world = await CreateWorldAsync(factory);

        var answer = await NameAsync(world, new { teamId = world.Mars.ToString() });

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null(answer.MediaType);
        Assert.Equal(world.Mars, await SettingAsync(world.ProjectId));
        var marks = await MarksAsync(world);
        Assert.Equal("named", Assert.Single(marks, team => team.Id == world.Mars).Mark);
        Assert.All(marks.Where(team => team.Id != world.Mars), team => Assert.Null(team.Mark));
    }

    // US-3 · "the administrator chooses the default", the role: «Участник» holds no organizations.team.write.
    [Fact]
    public async Task Name_Member_Returns403()
    {
        var world = await CreateWorldAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var reader = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);

        var asMember = await NameAsync(world, new { teamId = world.Mars.ToString() }, member.Token);
        var asReader = await NameAsync(world, new { teamId = world.Mars.ToString() }, reader.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (asMember.Status, asMember.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (asReader.Status, asReader.ErrorCode));
        Assert.Null(await SettingAsync(world.ProjectId));
    }

    // US-3 · outline "refusals and roles": another project's team 409, a deleted, a deactivated project's, an unknown, a malformed
    // and another organization's team 404 — the setting is as before.
    [Theory]
    [InlineData("otherProject", HttpStatusCode.Conflict, TeamNotInProject)]
    [InlineData("deleted", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("deactivatedProject", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("unknown", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("malformed", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("foreign", HttpStatusCode.NotFound, TeamNotFound)]
    public async Task Name_Refusals_SettingUnchanged(string which, HttpStatusCode status, string code)
    {
        var world = await CreateWorldAsync(factory);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {world.Venus} WHERE id = {world.ProjectId}");
        var named = which switch
        {
            "otherProject" => world.OtherProjectTeam.ToString(),
            "deleted" => world.DeletedTeam.ToString(),
            "deactivatedProject" => world.DeactivatedProjectTeam.ToString(),
            "foreign" => world.OtherOrganizationTeam.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => "not-an-id",
        };

        var answer = await NameAsync(world, new { teamId = named });

        Assert.Equal((status, code), (answer.Status, answer.ErrorCode));
        Assert.Equal(world.Venus, await SettingAsync(world.ProjectId));
    }

    // BR-OT-16 · another organization's team answers exactly as an unknown id.
    [Fact]
    public async Task Name_ForeignTeam_AnswersExactlyAsUnknown()
    {
        var world = await CreateWorldAsync(factory);

        var foreign = await NameAsync(world, new { teamId = world.OtherOrganizationTeam.ToString() });
        var unknown = await NameAsync(world, new { teamId = Guid.CreateVersion7().ToString() });

        Assert.Equal((unknown.Status, unknown.ErrorCode, unknown.Detail, unknown.MediaType), (foreign.Status, foreign.ErrorCode, foreign.Detail, foreign.MediaType));
    }

    // api-contract § 0.4 · the project's 404 comes before the team's: an unknown project answers its own code.
    [Fact]
    public async Task Name_UnknownProject_AnswersTheProjectNotFound_BeforeTheTeam()
    {
        var world = await CreateWorldAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Project(world.OrganizationId, Guid.CreateVersion7())}/default-team",
            new { teamId = Guid.CreateVersion7().ToString() },
            OrganizationsTokens.SignedIn());

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (answer.Status, answer.ErrorCode));
    }

    // US-3 · outline "losing the named team", the clearing: `{ "teamId": null }`, `{}` and no body all clear the setting (D-7).
    [Theory]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("nobody")]
    public async Task Name_NoTeam_ClearsTheSetting(string how)
    {
        var world = await CreateWorldAsync(factory);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {world.Mars} WHERE id = {world.ProjectId}");
        object? body = how switch
        {
            "null" => new { teamId = (string?)null },
            "empty" => new { },
            _ => null,
        };

        var answer = await NameAsync(world, body);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null(await SettingAsync(world.ProjectId));
        Assert.All(await MarksAsync(world), team => Assert.Null(team.Mark));
    }

    // US-3 · "the default affects the creation only": naming a team moves no item and changes no view, filter or order.
    [Fact]
    public async Task Name_MovesNoItem_ChangesNoViewOrFilter()
    {
        var world = await CreateWorldAsync(factory);
        var story = await CreateAsync(factory, world.Items, "US", "История");
        await SetOwningTeamAsync(factory, story.Id, world.Venus);
        var before = await StoredAsync(factory, world.ProjectId, story.Identifier);
        var viewBefore = await WorkItemsTestData.ListAsync(factory, world.Items, "?teamId=" + world.Venus);

        var answer = await NameAsync(world, new { teamId = world.Mars.ToString() });

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(before, await StoredAsync(factory, world.ProjectId, story.Identifier));
        Assert.Equal(viewBefore.Body.GetRawText(), (await WorkItemsTestData.ListAsync(factory, world.Items, "?teamId=" + world.Venus)).Body.GetRawText());
    }

    // US-3 · "naming the current default again changes nothing".
    [Fact]
    public async Task Name_TheSameTeamAgain_IsANoOp()
    {
        var world = await CreateWorldAsync(factory);
        await NameAsync(world, new { teamId = world.Mars.ToString() });

        var again = await NameAsync(world, new { teamId = world.Mars.ToString() });

        Assert.Equal(HttpStatusCode.NoContent, again.Status);
        Assert.Equal(world.Mars, await SettingAsync(world.ProjectId));
    }

    // US-3 · "two teams and none named; one team; named wins": the teams read marks the named team «named»; with no team named,
    // a project's only team is «onlyTeam»; a second team ends the inference.
    [Fact]
    public async Task Marks_NamedOnlyTeamNone()
    {
        var world = await CreateWorldAsync(factory);
        var single = await OrganizationsTestData.CreateProjectAsync(factory, world.OrganizationId, "Одна", "ONE");
        var singleTeams = $"{OrganizationsApi.Teams(world.OrganizationId, single.Project.Id)}";

        var alone = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, singleTeams, token: OrganizationsTokens.SignedIn());
        Assert.Equal("onlyTeam", alone.Body.EnumerateArray().Single().GetProperty("defaultMark").GetString());

        var added = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, singleTeams, new { name = "Вторая" }, OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, added.Status);

        var several = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, singleTeams, token: OrganizationsTokens.SignedIn());
        Assert.All(several.Body.EnumerateArray(), team => Assert.Equal(System.Text.Json.JsonValueKind.Null, team.GetProperty("defaultMark").ValueKind));

        Assert.All(await MarksAsync(world), team => Assert.Null(team.Mark));
    }

    // mod-017 BR-OT-4 · the setting survives a deactivated project and its restore (the project answers 404 meanwhile).
    [Fact]
    public async Task Setting_SurvivesDeactivateAndRestore()
    {
        var world = await CreateWorldAsync(factory);
        await NameAsync(world, new { teamId = world.Mars.ToString() });

        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);
        var whileDeactivated = await NameAsync(world, new { teamId = world.Venus.ToString() });
        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Active);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (whileDeactivated.Status, whileDeactivated.ErrorCode));
        Assert.Equal(world.Mars, await SettingAsync(world.ProjectId));
    }

    // BR-OT-13 (1) · a naming and the deletion of the team it names, in both orders, never end with a setting that names a deleted
    // team: the naming first is cleared by the deletion; the deletion first refuses the naming.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Name_VersusDeleteTeam_BothOrders_NeverNamesADeletedTeam(bool namingFirst)
    {
        var world = await CreateWorldAsync(factory);
        using var gate = factory.CommitGates.Open();
        var teamPath = $"{OrganizationsApi.Teams(world.OrganizationId, world.ProjectId)}/{world.Mars}";

        Task<Answer> Naming(CommitGate? at) => OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, Path(world), new { teamId = world.Mars.ToString() }, OrganizationsTokens.SignedIn(), at);
        Task<Answer> Deletion(CommitGate? at) => OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, teamPath, token: OrganizationsTokens.SignedIn(), gate: at);

        var first = namingFirst ? Naming(gate) : Deletion(gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first request finished before reaching its commit gate.");
        var second = namingFirst ? Deletion(null) : Naming(null);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second request did not wait for the first one's lock of the team.");

        gate.Release();
        var (a, b) = (await first, await second);
        var (naming, deletion) = namingFirst ? (a, b) : (b, a);

        Assert.Equal(HttpStatusCode.NoContent, deletion.Status);
        Assert.Equal(namingFirst ? HttpStatusCode.NoContent : HttpStatusCode.NotFound, naming.Status);
        Assert.Null(await SettingAsync(world.ProjectId));
    }
}
