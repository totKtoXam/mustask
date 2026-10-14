using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetMembers;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/members</c> (api-contract § 1.2; US-1 "An inactive or unknown member is
/// shown as such" and "Past 500 ids the list and the member searches still answer"): every member of the organization
/// with the people named in «Добавлен» and «Роль изменена».
/// </summary>
/// <remarks>
/// <b>The order of answers</b> (api-contract § 0.1): the scoped check has answered 401, 404 and 403 already; the
/// handler parses the organization id again only to name its container. The address ends at the organization, so
/// nothing else can refuse a read.
/// <b>Reads, not locks.</b> A fixed number of queries whatever the size (the read model's remarks): the memberships,
/// then the projection lookups in batches of 500, never a call to Keycloak. A member whose account is disabled stays
/// listed (<c>inactive</c>), and one the projection does not know is listed <c>unknown</c>, after every member with a
/// name (BR-M1-17).
/// </remarks>
public sealed class GetMembersHandler(IMemberReadModel members)
{
    public async Task<GetMembersResult> Handle(GetMembersQuery query, CancellationToken cancellationToken)
    {
        // 1. The address.
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        return new GetMembersResult(await members.ListAsync(organizationId, cancellationToken));
    }
}
