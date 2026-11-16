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

    // BR-13 · the matrix over every write route: the team «Участник» and «Менеджер» and the organization «Администратор» write; the
    // team «Читатель», the organization «Участник» and «Читатель» without a grant do not; everyone reads.
    [Theory]
    [InlineData(GrantRole.Member, OrganizationRole.Viewer, true)]
    [InlineData(GrantRole.Manager, OrganizationRole.Viewer, true)]
    [InlineData(null, OrganizationRole.Admin, true)]
    [InlineData(GrantRole.Reader, OrganizationRole.Viewer, false)]
    [InlineData(GrantRole.Reader, OrganizationRole.Member, false)]
    [InlineData(null, OrganizationRole.Member, false)]
    [InlineData(null, OrganizationRole.Viewer, false)]
    public async Task EveryRoute_FollowsTheRoleMatrix(GrantRole? grant, OrganizationRole organizationRole, bool mayManage)
    {
        var world = await CreateWorldAsync(factory);
        var team = world.PlatformId;
        var holder = await HolderAsync(factory, world, team, grant, organizationRole);
        var renamed = await NewAsync(factory, world, team, "Переименовать");
        var removable = await NewAsync(factory, world, team, "Удалить");
        var startable = await NewAsync(factory, world, team, "Начать", "2026-10-05", "2026-10-16");
        var closing = await ActiveAsync(factory, world, world.ProjectTeamId, "Закрыть", "2026-09-07", "2026-09-18");
        var closedId = await ActiveAsync(factory, world, team, "Закрытый", "2026-08-03", "2026-08-14");
        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, team, closedId)).Status);

        var writes = new (HttpMethod Method, string Path, object? Body, HttpStatusCode Ok)[]
        {
            (HttpMethod.Put, Sprint(world, team, renamed), new { name = "Новое" }, HttpStatusCode.OK),
            (HttpMethod.Delete, Sprint(world, team, removable), null, HttpStatusCode.NoContent),
            (HttpMethod.Post, $"{Sprint(world, team, startable)}/start", null, HttpStatusCode.OK),
        };

        foreach (var write in writes)
        {
            var answer = await SendAsync(factory, write.Method, write.Path, write.Body, holder.Token);
            Assert.Equal(mayManage ? write.Ok : HttpStatusCode.Forbidden, answer.Status);
        }

        // The close of an Active sprint of the team (the started one when the holder may start).
        var active = mayManage ? startable : await ActiveAsync(factory, world, team, "Активный", "2026-09-07", "2026-09-18");
        var close = await SendAsync(factory, HttpMethod.Post, $"{Sprint(world, team, active)}/close", new { }, holder.Token);
        Assert.Equal(mayManage ? HttpStatusCode.OK : HttpStatusCode.Forbidden, close.Status);
        _ = closing;

        foreach (var path in new[] { Sprints(world, team), Sprint(world, team, closedId), $"{Sprint(world, team, closedId)}/dynamics", $"{Sprint(world, team, closedId)}/participants", $"{Sprint(world, team, closedId)}/items" })
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(factory, HttpMethod.Get, path, token: holder.Token)).Status);
        }

        if (!mayManage)
        {
            var preview = await SendAsync(factory, HttpMethod.Get, $"{Sprint(world, team, active)}/close-preview", token: holder.Token);
            Assert.Equal(HttpStatusCode.OK, preview.Status);
        }
    }

    [Fact]
    public async Task ATeamThatIsNotInTheOrganization_Is404()
    {
        var world = await CreateWorldAsync(factory);

        var answer = await SendAsync(factory, HttpMethod.Get, Sprints(world, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
    }
}
