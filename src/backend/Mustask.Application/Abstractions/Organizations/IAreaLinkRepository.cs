using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The write side of the area links (mod-018 BR-AW-6, ADR-0028 rule 4): the area–team link (departments only, OQ-8 A). A link has no binder — no item
/// or setting keys onto it — so an unlink takes no link lock: one <c>DELETE</c> is enough, and an area deleted at the same moment
/// cascades the link it holds. A bind takes the area <c>FOR SHARE</c> and the team <c>FOR SHARE</c> first (the callers do), so a link
/// of an area or a team being deleted is refused as not found. Every statement carries the organization.
/// </summary>
public interface IAreaLinkRepository
{
    /// <summary>
    /// Links every pair not linked yet with one statement (<c>INSERT … SELECT unnest … ON CONFLICT DO NOTHING</c>): a pair already linked
    /// keeps who linked it and when, and two requests that link one pair leave one row. Answers the number of links made.
    /// </summary>
    Task<int> LinkTeamsAsync(
        Guid organizationId,
        IReadOnlyCollection<(Guid AreaId, Guid TeamId)> pairs,
        string linkedBy,
        DateTimeOffset linkedAt,
        CancellationToken cancellationToken);

    /// <summary>Removes the link, if there is one. A pair that is not linked is a success that changes nothing.</summary>
    Task RemoveTeamLinkAsync(Guid organizationId, Guid areaId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>Removes every area link of the team, with one statement (a team's deletion, BR-AW-10).</summary>
    Task RemoveTeamLinksAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>Adds a link whose department or team is created in the same operation.</summary>
    void Add(AreaTeam link);

    /// <inheritdoc cref="IOrganizationRepository.SaveAsync"/>
    Task SaveAsync(CancellationToken cancellationToken);
}
