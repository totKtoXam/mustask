using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetGrants;

/// <summary>
/// The grants of a department, a project or a team (mod-019 api-contract § 2; US-1 "An object's grants are read by those who read
/// the object", US-5). A project's list also carries the rows inherited from the department above its node, while the node's
/// switch is on (BR-SA-5, OQ-D2), each marked with its origin; the node's switch itself is in <c>inheritance</c>.
/// </summary>
/// <remarks>
/// A fixed number of statements: the object, the grants (and the department's, for a project), the node's chain, and the people
/// named in one lookup. A grant held by an inactive user is listed as inactive, an unknown one as unknown (BR-M1-32).
/// </remarks>
public sealed class GetGrantsHandler(IGrantRepository grants, IUserDirectory directory)
{
    public async Task<GetGrantsResponse> Handle(GetGrantsQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ObjectId, out var objectId))
        {
            throw new NotFoundException(GrantObjects.NotFoundCodeOf(query.Scope));
        }

        switch (await grants.ResolveAsync(query.Scope, organizationId, objectId, lockRow: false, cancellationToken))
        {
            case GrantObjectState.NotFound:
                throw new NotFoundException(GrantObjects.NotFoundCodeOf(query.Scope));
            case GrantObjectState.ScopeNotAllowed:
                // A project node or a sub-area holds none: the same refusal as a write (BR-SA-4).
                throw new ConflictException(OrganizationErrorCodes.GrantScopeNotAllowed);
        }

        var held = await grants.ListAsync(query.Scope, objectId, cancellationToken);
        ProjectInheritance? inheritance = null;
        IReadOnlyList<GrantRecord> inherited = [];

        if (query.Scope == ResourceScope.Project)
        {
            inheritance = await grants.FindProjectInheritanceAsync(organizationId, objectId, cancellationToken);

            if (inheritance is { InheritsAccess: true })
            {
                inherited = await grants.ListAsync(ResourceScope.Area, inheritance.DepartmentId, cancellationToken);
            }
        }

        var ids = held.Concat(inherited).SelectMany(grant => new[] { grant.UserId, grant.GrantedBy }).Distinct(StringComparer.Ordinal).ToList();
        var lookup = ids.Count == 0
            ? new UserLookupResult(new Dictionary<string, DirectoryUser>(), [])
            : await UserDirectoryBatches.LookupAllAsync(directory, ids, cancellationToken);

        var department = inheritance is null ? null : new DepartmentRefView(inheritance.DepartmentId, inheritance.DepartmentName);
        var rows = held.Select(grant => GrantRows.Of(grant, GrantOrigins.Held, null, lookup.Known))
            .Concat(inherited.Select(grant => GrantRows.Of(grant, GrantOrigins.Department, department, lookup.Known)));

        return new GetGrantsResponse(
            GrantRows.Order(rows, lookup.Known),
            inheritance is null ? null : new GrantInheritanceView(inheritance.DepartmentId, inheritance.DepartmentName, inheritance.InheritsAccess));
    }
}
