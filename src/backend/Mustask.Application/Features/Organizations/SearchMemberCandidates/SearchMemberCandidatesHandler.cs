using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SearchMemberCandidates;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/member-candidates?text=</c> (api-contract § 1.3; US-1 "The search needs two
/// characters", "More than twenty matches are cut and said so", "An inactive or unknown user is neither offered nor
/// added"): the «Добавление участника» search over the whole user projection.
/// </summary>
/// <remarks>
/// The candidates are <b>active</b> projected users, whoever they already are: an existing member is offered and refused
/// on add (status.md D-16 OP-6, doc-006 US-3). The search reads the projection only, so it works with Keycloak stopped
/// (mod-014 BR-UP-9), and mod-014 owns the rules of the search itself: at least 2 characters, at most 20 answered in its
/// order, and its own 400 for a text over 100 characters, passed through unchanged.
/// </remarks>
public sealed class SearchMemberCandidatesHandler(IUserDirectory directory)
{
    public async Task<MemberCandidatesResponse> Handle(SearchMemberCandidatesQuery query, CancellationToken cancellationToken)
    {
        // 1. The address: the scoped check has answered; the handler only refuses what it cannot read.
        if (!AddressedId.TryParse(query.OrganizationId, out _))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        var result = await directory.SearchAsync(new UserSearch(query.Text ?? string.Empty, IncludeInactive: false), cancellationToken);

        return new MemberCandidatesResponse(
            result.Users.Select(user => new MemberCandidateResponse(user.Id, user.DisplayName, user.Email)).ToList(),
            result.MoreMatched);
    }
}
