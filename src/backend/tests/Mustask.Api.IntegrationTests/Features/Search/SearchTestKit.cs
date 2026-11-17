using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Search;

/// <summary>What a search answer shows: the groups in order, each with its flag and its row names.</summary>
internal sealed record SearchGroupView(string Kind, bool HasMore, IReadOnlyList<string> Names, IReadOnlyList<bool> Similar);

/// <summary>Helpers shared by the search read's integration tests (feat-071).</summary>
internal static class SearchTestKit
{
    public static string Path(Guid organizationId) => $"/api/organizations/{organizationId}/search";

    public static Task<Answer> SearchAsync(MustaskApiFactory factory, Guid organizationId, string? token, string? query, string? expand = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, Path(organizationId), new { query, expand }, token);

    public static IReadOnlyList<SearchGroupView> Groups(Answer answer)
    {
        Assert.Equal(System.Net.HttpStatusCode.OK, answer.Status);

        return answer.Body.GetProperty("groups").EnumerateArray().Select(group => new SearchGroupView(
            group.GetProperty("kind").GetString()!,
            group.GetProperty("hasMore").GetBoolean(),
            group.GetProperty("rows").EnumerateArray().Select(Name).ToList(),
            group.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("similar").GetBoolean()).ToList())).ToList();
    }

    private static string Name(JsonElement row) =>
        (row.TryGetProperty("name", out var name) ? name : row.GetProperty("displayName")).GetString()!;

    public static IReadOnlyList<string> Names(Answer answer, string kind) =>
        Groups(answer).FirstOrDefault(group => group.Kind == kind)?.Names ?? [];

    /// <summary>A project placed under a department (or at the top level), visible to the organization or not.</summary>
    public static Task<NewProject> ProjectAsync(
        MustaskApiFactory factory, Guid organizationId, Area? department, string name, string key, bool visible = true) =>
        OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var created = Project.Create(
                organizationId, OrganizationsTestData.Name(name), key, OrganizationsTestData.Now, "seed", "teal", parentAreaId: department?.Id);
            created.Project.SetVisibleToOrganization(visible);
            projects.Add(created);
            await projects.SaveAsync(ct);

            return created;
        });

    /// <summary>A person in the projection who is a current member of the organization.</summary>
    public static async Task<MembershipTestData.TestMember> MemberAsync(
        MustaskApiFactory factory, Guid organizationId, string first, string last, string email, bool enabled = true,
        Mustask.Domain.Identity.OrganizationRole role = Mustask.Domain.Identity.OrganizationRole.Member)
    {
        var member = await MembershipTestData.NewMemberAsync(factory, organizationId, role);
        await UserDirectoryTestData.SeedAsync(factory, [UserDirectoryTestData.Person(member.Id, first, last, email, enabled)]);

        return member;
    }
}
