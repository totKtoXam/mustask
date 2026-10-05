using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetOrganization;

/// <summary>
/// <c>GET /api/organizations/{organizationId}</c> (US-2 "organization settings",
/// US-1 "every signed-in user sees every organization"): the one active
/// organization, as stored.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4): the
/// address is the only thing a read carries, so the organization level is the
/// whole answer. The organization-context filter has already refused an id that
/// is malformed, unknown or deactivated; the handler asks the same question
/// again through the read model's one intent, <c>FindActiveAsync</c>, so that
/// it never serves an organization the filter would not (ADR-0026 rule 4: an
/// unreachable object answers its level's not-found code, never "deactivated").
/// </para>
/// <para>
/// <b>No lock, no transaction</b>: plan-backend § 8.2 lists locks for writes only. The
/// interim guard (BR-M1-19) lets any signed-in user through, so no right is
/// read here.
/// </para>
/// </remarks>
public sealed class GetOrganizationHandler(IOrganizationReadModel organizations)
{
    public async Task<GetOrganizationResult> Handle(GetOrganizationQuery query, CancellationToken cancellationToken)
    {
        // 1. The address: a malformed or unknown id, and a deactivated organization, all answer organization.not_found
        // (BR-M1-20, ADR-0026 rule 5).
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        var organization = await organizations.FindActiveAsync(organizationId, cancellationToken)
            ?? throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);

        return new GetOrganizationResult(new OrganizationResponse(organization.Id, organization.Name));
    }
}
