using System.Net;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Iterations.IterationsTestData;

namespace Mustask.Api.IntegrationTests.Features.Iterations;

/// <summary>
/// Who may do what with a team's sprints (mod-007 US-1 to US-3, ADR-0038): the writes need <c>iterations.sprint.manage</c> on the
/// team, which the organization's administrator and the team's Member and Manager hold; the reads need only the team's read code.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SprintAccessEndpointTests(MustaskApiFactory factory)
{
    [Theory]
    [InlineData(null, OrganizationRole.Member, HttpStatusCode.Forbidden)]
    [InlineData(null, OrganizationRole.Viewer, HttpStatusCode.Forbidden)]
    [InlineData(GrantRole.Reader, OrganizationRole.Viewer, HttpStatusCode.Forbidden)]
    [InlineData(GrantRole.Member, OrganizationRole.Viewer, HttpStatusCode.Created)]
    [InlineData(GrantRole.Manager, OrganizationRole.Viewer, HttpStatusCode.Created)]
    public async Task Create_NeedsTheSprintCodeOnTheTeam(GrantRole? grant, OrganizationRole organizationRole, HttpStatusCode expected)
    {
        var world = await CreateWorldAsync(factory);
        var holder = await HolderAsync(factory, world, world.PlatformId, grant, organizationRole);

        var answer = await CreateAsync(factory, world, world.PlatformId, token: holder.Token);

        Assert.Equal(expected, answer.Status);

        // A grant on one team does not reach another.
        var other = await CreateAsync(factory, world, world.ProjectTeamId, token: holder.Token);
        Assert.Equal(HttpStatusCode.Forbidden, other.Status);
    }

    [Fact]
    public async Task EveryWrite_IsRefusedToAMemberWithoutAGrant_AndTheReadsAreServed()
    {
        var world = await CreateWorldAsync(factory);
        var sprint = await NewAsync(factory, world, world.PlatformId);
        var member = await HolderAsync(factory, world, world.PlatformId, null, OrganizationRole.Member);

        var writes = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Put, Sprint(world, world.PlatformId, sprint), new { name = "Х" }),
            (HttpMethod.Post, $"{Sprint(world, world.PlatformId, sprint)}/start", null),
            (HttpMethod.Post, $"{Sprint(world, world.PlatformId, sprint)}/close", new { }),
            (HttpMethod.Delete, Sprint(world, world.PlatformId, sprint), null),
        };

        foreach (var write in writes)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(factory, write.Method, write.Path, write.Body, member.Token)).Status);
        }

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(factory, HttpMethod.Get, Sprints(world, world.PlatformId), token: member.Token)).Status);
    }

    [Fact]
    public async Task ATeamThatIsNotInTheOrganization_Is404()
    {
        var world = await CreateWorldAsync(factory);

        var answer = await SendAsync(factory, HttpMethod.Get, Sprints(world, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
    }
}
