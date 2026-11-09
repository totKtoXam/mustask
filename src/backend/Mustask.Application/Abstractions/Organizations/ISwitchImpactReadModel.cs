using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>A project grant given by one of the users who stop being reached (mod-019 BR-SA-19, R-27).</summary>
public sealed record GivenGrantRecord(string UserId, GrantRole Role, string GivenBy);

/// <summary>What turning «Наследовать доступ» off on a node would do now (api-contract § 3, <c>SwitchImpact</c>).</summary>
/// <param name="Type">The node's type.</param>
/// <param name="ParentId">The node's parent; <c>null</c> at the top level.</param>
/// <param name="InheritsAccess">The node's current value.</param>
/// <param name="AffectedUserIds">Distinct users who hold a grant that reaches the node now and would not after; administrators and holders of a project grant on their own excluded.</param>
/// <param name="GivenGrants">The owning project's held grants given by those users, at most <see cref="SwitchImpactLimits.GivenGrantsListed"/>.</param>
/// <param name="GivenGrantsTotal">The count behind <paramref name="GivenGrants"/>.</param>
public sealed record SwitchImpactData(
    AreaType Type,
    Guid? ParentId,
    bool InheritsAccess,
    IReadOnlyList<string> AffectedUserIds,
    IReadOnlyList<GivenGrantRecord> GivenGrants,
    int GivenGrantsTotal);

public static class SwitchImpactLimits
{
    public const int GivenGrantsListed = 50;
}

/// <summary>The switch-off impact read (mod-019 BR-SA-7, OQ-6, OQ-D1): a few statements over the tree and the grants, nothing cached.</summary>
public interface ISwitchImpactReadModel
{
    /// <summary><c>null</c> when the node is not in the organization.</summary>
    Task<SwitchImpactData?> ReadAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken);
}
