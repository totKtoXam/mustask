using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>Reads of teams through the reachability specification (ADR-0026 rules 3 and 4).</summary>
public interface ITeamReadModel
{
    /// <summary>
    /// The active teams of a reachable project, <b>by name</b> in Russian collation, letter case not deciding, then by id
    /// (mod-017 api-contract § 1.3, BR-OT-11): no team comes first. Empty when the project is not reachable. Each row says
    /// whether <paramref name="userId"/> is in that team (mod-016 BR-M1-27, D-5) and which default mark the team carries
    /// (BR-OT-4): read with the list, in the same query, so a change counts from the next read.
    /// </summary>
    Task<IReadOnlyList<TeamRow>> ListActiveAsync(Guid organizationId, Guid projectId, string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The team when it is active in a reachable project; otherwise <c>null</c> (a deleted team included).
    /// The row's <see cref="TeamRow.IsMember"/> is always <c>false</c>: the single-team read does not carry it.
    /// </summary>
    Task<TeamRow?> FindReachableAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken);
}

/// <summary>
/// One active team (mod-017 api-contract § 1.3): no first-team flag and no default area. <paramref name="Mark"/> is the team's
/// default mark, computed with <c>ProjectDefaultTeam.MarkOf</c> in the statement. <paramref name="IsMember"/> is the acting
/// user's membership of the team, set only by the list (mod-016).
/// </summary>
public sealed record TeamRow(Guid Id, Guid ProjectId, string Name, DefaultTeamMark Mark, bool IsMember = false);

/// <summary>The wire names of the default mark (api-contract § 1.3): <c>named</c>, <c>onlyTeam</c>, and <c>null</c> for neither.</summary>
public static class DefaultTeamMarkNames
{
    public static string? ToWireName(this DefaultTeamMark mark) =>
        mark switch
        {
            DefaultTeamMark.Named => "named",
            DefaultTeamMark.OnlyTeam => "onlyTeam",
            _ => null,
        };
}
