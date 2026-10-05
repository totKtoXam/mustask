using Mustask.Application.Abstractions.UserDirectory;

namespace Mustask.Application.Features.WorkItems.GetAssigneeCandidates;

/// <summary>
/// The «Исполнитель» picker's search (US-4, BR-M3-22). Until mod-016 the
/// candidates are every active user of mod-014's directory (D-5, OQ-3), found
/// by display name or e-mail; mod-014 owns the rules of the search itself: at
/// least 2 characters, at most 20 answered in its order, and its own refusal of
/// a text over 100 characters, passed through unchanged. An inactive user is
/// never offered.
/// </summary>
/// <remarks>
/// The organization-context filter has answered for the organization: nothing
/// here depends on it until mod-016 narrows the candidates to its members.
/// </remarks>
public sealed class GetAssigneeCandidatesHandler(IUserDirectory directory)
{
    public async Task<AssigneeCandidatesResponse> Handle(GetAssigneeCandidatesQuery query, CancellationToken cancellationToken)
    {
        var result = await directory.SearchAsync(new UserSearch(query.Text ?? string.Empty, IncludeInactive: false), cancellationToken);

        return new AssigneeCandidatesResponse(
            result.Users.Select(user => new AssigneeCandidateResponse(user.Id, user.DisplayName, user.Email)).ToList(),
            result.MoreMatched);
    }
}
