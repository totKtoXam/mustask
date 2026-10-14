using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.WorkItems.GetAssigneeCandidates;

/// <summary>
/// The «Исполнитель» picker's search (US-4, BR-M3-22; mod-016 US-5 "The picker offers only active members of the item's
/// organization", BR-M1-26): the active <b>members of the item's organization</b>, found by display name or e-mail. A
/// disabled member, a member of another organization and a projected user in none are not offered; any role may be
/// assigned. mod-014 owns the rules of the search itself: at least 2 characters, at most 20 answered in its order, and
/// its own refusal of a text over 100 characters, passed through unchanged.
/// </summary>
/// <remarks>
/// The search is restricted to the organization's member ids with mod-014's <c>AmongIds</c>, split above 500 ids into
/// batches and merged (<see cref="UserDirectoryBatches"/>, BR-UP-17): an organization of 501 members still answers. It
/// reads the memberships and the projection only, so it works with Keycloak stopped.
/// </remarks>
public sealed class GetAssigneeCandidatesHandler(IUserDirectory directory, IMemberReadModel members)
{
    public async Task<AssigneeCandidatesResponse> Handle(GetAssigneeCandidatesQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        var memberIds = await members.ListMemberIdsAsync(organizationId, cancellationToken);
        var result = await UserDirectoryBatches.SearchAmongAsync(directory, query.Text ?? string.Empty, memberIds, cancellationToken);

        return new AssigneeCandidatesResponse(
            result.Users.Select(user => new AssigneeCandidateResponse(user.Id, user.DisplayName, user.Email)).ToList(),
            result.MoreMatched);
    }
}
