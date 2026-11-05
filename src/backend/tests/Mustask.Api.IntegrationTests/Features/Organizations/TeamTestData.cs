using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// The requests and the reads of the organization-level teams' endpoint tests (feat-028): the team routes at the organization's address,
/// the project's link and unlink, and what is stored — the teams of an organization, the links of a project or a team, a default setting.
/// Every read goes to the database through a scope of the shared host, so a test asserts what the request left, not what it answered.
/// </summary>
internal static class TeamTestData
{
    public static string Team(Guid organizationId, Guid teamId) => $"{OrganizationsApi.OrgTeams(organizationId)}/{teamId}";

    public static string ProjectTeam(Guid organizationId, Guid projectId, Guid teamId) => $"{OrganizationsApi.Teams(organizationId, projectId)}/{teamId}";

    public static Task<Answer> CreateTeamAsync(
        MustaskApiFactory factory, Guid organizationId, object body, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, OrganizationsApi.OrgTeams(organizationId), body, token ?? OrganizationsTokens.SignedIn(), gate);

    public static Task<Answer> GetAsync(MustaskApiFactory factory, string path, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: token ?? OrganizationsTokens.SignedIn());

    public static Task<Answer> DeleteTeamAsync(
        MustaskApiFactory factory, Guid organizationId, Guid teamId, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Delete, Team(organizationId, teamId), token: token ?? OrganizationsTokens.SignedIn(), gate: gate);

    public static Task<Answer> UnlinkAsync(
        MustaskApiFactory factory, Guid organizationId, Guid projectId, Guid teamId, object? body, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{ProjectTeam(organizationId, projectId, teamId)}/unlink", body, token ?? OrganizationsTokens.SignedIn(), gate);

    /// <summary>An unlink body naming a destination team, or «Без команды» for <c>null</c>.</summary>
    public static object To(Guid? teamId) => new { destination = new { teamId = teamId?.ToString() } };

    public static Task<Answer> LinkTeamsAsync(
        MustaskApiFactory factory, Guid organizationId, Guid projectId, object body, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, OrganizationsApi.Teams(organizationId, projectId), body, token ?? OrganizationsTokens.SignedIn(), gate);

    public static Task<Answer> LinkProjectsAsync(
        MustaskApiFactory factory, Guid organizationId, Guid teamId, object body, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{Team(organizationId, teamId)}/projects", body, token ?? OrganizationsTokens.SignedIn(), gate);

    /// <summary>
    /// Two requests that meet on a lock (the pattern of every race test, ADR-0011 echelon 3): the first is held at its commit gate with
    /// its locks, the second is sent and must block on one of them, then the first is released and both answers are returned.
    /// </summary>
    public static async Task<(Answer First, Answer Second)> RaceAsync(
        MustaskApiFactory factory, Func<CommitGate?, Task<Answer>> first, Func<CommitGate?, Task<Answer>> second)
    {
        using var gate = factory.CommitGates.Open();

        var firstTask = first(gate);
        await Task.WhenAny(gate.Reached, firstTask);
        Assert.False(firstTask.IsCompleted, "The first request finished before reaching its commit gate.");

        var secondTask = second(null);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, secondTask);
        Assert.False(secondTask.IsCompleted, "The second request did not wait for the first one.");

        gate.Release();

        return (await firstTask, await secondTask);
    }

    /// <summary>The teams of an organization of every status, as stored now.</summary>
    public static async Task<IReadOnlyList<Team>> StoredTeamsAsync(MustaskApiFactory factory, Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Teams
            .AsNoTracking()
            .Where(team => team.OrganizationId == organizationId)
            .ToListAsync();
    }

    public static async Task<Team> StoredTeamAsync(MustaskApiFactory factory, Guid teamId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Teams.AsNoTracking().SingleAsync(team => team.Id == teamId);
    }

    /// <summary>Every link of a project, as stored now.</summary>
    public static async Task<IReadOnlyList<ProjectTeam>> LinksOfProjectAsync(MustaskApiFactory factory, Guid projectId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().ProjectTeams
            .AsNoTracking()
            .Where(link => link.ProjectId == projectId)
            .OrderBy(link => link.TeamId)
            .ToListAsync();
    }

    /// <summary>Every link of a team, as stored now.</summary>
    public static async Task<IReadOnlyList<ProjectTeam>> LinksOfTeamAsync(MustaskApiFactory factory, Guid teamId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().ProjectTeams
            .AsNoTracking()
            .Where(link => link.TeamId == teamId)
            .OrderBy(link => link.ProjectId)
            .ToListAsync();
    }

    /// <summary>The project's description as stored.</summary>
    public static async Task<string?> ProjectDescriptionAsync(MustaskApiFactory factory, Guid projectId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.Description)
            .SingleAsync();
    }

    /// <summary>The project's default-team setting as stored (not as resolved).</summary>
    public static async Task<Guid?> SettingAsync(MustaskApiFactory factory, Guid projectId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.DefaultTeamId)
            .SingleAsync();
    }

    public static async Task<IReadOnlyList<string>> TeamMemberIdsAsync(MustaskApiFactory factory, Guid teamId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == teamId)
            .Select(membership => membership.UserId)
            .OrderBy(userId => userId)
            .ToListAsync();
    }

    /// <summary>The number of items a team owns, in any state and any project, as stored.</summary>
    public static async Task<int> OwnedItemCountAsync(MustaskApiFactory factory, Guid teamId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().WorkItems.CountAsync(item => item.OwningTeamId == teamId);
    }

    /// <summary>The ids of the elements of a JSON array of objects that have an <c>id</c>, in the order received.</summary>
    public static List<Guid> Ids(JsonElement array) =>
        array.EnumerateArray().Select(element => element.GetProperty("id").GetGuid()).ToList();

    public static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);

    /// <summary>The first reason code under a field of a 400 answer.</summary>
    public static string? ReasonOf(Answer answer, string field) =>
        answer.Body.GetProperty("errors").GetProperty(field).EnumerateArray().First().GetProperty("code").GetString();
}
