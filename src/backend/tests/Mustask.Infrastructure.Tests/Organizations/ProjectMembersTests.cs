using Mustask.Domain.Identity;
using Mustask.Infrastructure.Organizations;
using Mustask.Infrastructure.Tests.Process;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// mod-016 plan-backend § 3.10 against a real PostgreSQL: the one statement that the work-item and backlog reads add for the
/// assignee's <c>isMember</c> flag (BR-M1-26, US-5 "The reads say whether the assignee is still a member") — which of the ids a
/// page names hold a membership in the organization of the project, in any role, in a single statement, and none at all when there
/// is nothing to ask.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class ProjectMembersTests(OrganizationsDatabaseFixture database)
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private async Task<(HashSet<string> Members, int Statements)> AskAsync(Guid projectId, params string[] ids)
    {
        var counter = new CommandCountingInterceptor();
        await using var context = database.NewContext(counter);

        var members = await ProjectMembers.MemberIdsAsync(context, projectId, ids, CancellationToken.None);

        return (members, counter.Commands.Count);
    }

    // US-5 · "The reads say whether the assignee is still a member": members of any role are named, a user who is a member only of
    // ANOTHER organization, an ex-member and an unknown id are not — all in one statement.
    [Fact]
    public async Task MemberIds_NamesTheMembersOfTheProjectsOrganization_InAnyRole_InOneStatement()
    {
        var tag = Tag();
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        string[] roles = [$"admin-{tag}", $"member-{tag}", $"viewer-{tag}"];
        await database.AddMemberAsync(structure.Organization.Id, roles[0], OrganizationRole.Admin);
        await database.AddMemberAsync(structure.Organization.Id, roles[1], OrganizationRole.Member);
        await database.AddMemberAsync(structure.Organization.Id, roles[2], OrganizationRole.Viewer);
        await database.AddMemberAsync(other.Organization.Id, $"elsewhere-{tag}");

        var (members, statements) = await AskAsync(
            structure.Project.Project.Id, [.. roles, $"elsewhere-{tag}", $"nobody-{tag}"]);

        Assert.Equal(roles.Order(StringComparer.Ordinal), members.Order(StringComparer.Ordinal));
        Assert.Equal(1, statements);
    }

    // The same user is a member of the organization of one project and not of another's: the answer follows the project.
    [Fact]
    public async Task MemberIds_FollowTheProject_NotTheUser()
    {
        var tag = Tag();
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, $"user-{tag}");

        var here = await AskAsync(structure.Project.Project.Id, $"user-{tag}");
        var there = await AskAsync(other.Project.Project.Id, $"user-{tag}");

        Assert.Equal([$"user-{tag}"], here.Members);
        Assert.Empty(there.Members);
    }

    // Nothing to ask, nothing sent: a page with no assignee costs no statement.
    [Fact]
    public async Task MemberIds_NoIds_SendsNoStatement()
    {
        var structure = await database.CreateStructureAsync();

        var (members, statements) = await AskAsync(structure.Project.Project.Id);

        Assert.Empty(members);
        Assert.Equal(0, statements);
    }
}
