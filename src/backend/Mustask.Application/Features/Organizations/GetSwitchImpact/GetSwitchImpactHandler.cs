using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetSwitchImpact;

/// <summary>The query of <c>GET …/areas/{areaId}/inherits-access/impact</c>.</summary>
public sealed record GetSwitchImpactQuery(string? OrganizationId, string? AreaId);

/// <summary>A project grant given by a user who stops being reached (api-contract § 1, <c>SwitchImpact.givenGrants</c>).</summary>
public sealed record GivenGrantView(UserRefView User, string Role, UserRefView GivenBy);

/// <summary><c>SwitchImpact</c> of api-contract § 1.</summary>
public sealed record SwitchImpactResponse(int HolderCount, bool CallerIncluded, IReadOnlyList<GivenGrantView> GivenGrants, int GivenGrantsTotal);

/// <summary>
/// <c>GET …/areas/{areaId}/inherits-access/impact</c> (mod-019 api-contract § 3, US-2, OQ-6, OQ-D1, R-27): what turning the switch off
/// would do now — the count of distinct grant holders who stop being reached, whether the caller is one of them, and the owning
/// project's held grants given by those users (listed, never ended). Read once when the dialog opens; the confirm does not re-judge it.
/// Decided like the write: <c>area.write</c> on the node (the route), then on its parent (403), then the node's type (409).
/// </summary>
public sealed class GetSwitchImpactHandler(
    ISwitchImpactReadModel impact,
    IUserDirectory directory,
    IOrganizationAccess access,
    ICurrentUser currentUser)
{
    public async Task<SwitchImpactResponse> Handle(GetSwitchImpactQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.AreaId, out var areaId)
            || await impact.ReadAsync(organizationId, areaId, cancellationToken) is not { } data)
        {
            throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
        }

        var holdsParent = data.ParentId is { } parent
            ? await access.HoldsOnAsync(PermissionCatalog.OrganizationsAreaWrite, parent, cancellationToken)
            : access.Holds(PermissionCatalog.OrganizationsAreaCreate);

        if (!holdsParent)
        {
            throw new ForbiddenException(OrganizationErrorCodes.AccessForbidden);
        }

        if (data.Type == AreaType.Department)
        {
            throw new ConflictException(OrganizationErrorCodes.AreaTypeNotAllowed);
        }

        var ids = data.GivenGrants.SelectMany(grant => new[] { grant.UserId, grant.GivenBy }).Distinct(StringComparer.Ordinal).ToList();
        var lookup = ids.Count == 0
            ? new UserLookupResult(new Dictionary<string, DirectoryUser>(), [])
            : await UserDirectoryBatches.LookupAllAsync(directory, ids, cancellationToken);

        var given = data.GivenGrants
            .Select(grant => new GivenGrantView(UserRefView.Of(grant.UserId, lookup.Known), GrantRoles.ToCode(grant.Role), UserRefView.Of(grant.GivenBy, lookup.Known)))
            .ToList();

        return new SwitchImpactResponse(
            data.AffectedUserIds.Count,
            data.AffectedUserIds.Contains(currentUser.SubjectId, StringComparer.Ordinal),
            given,
            data.GivenGrantsTotal);
    }
}
