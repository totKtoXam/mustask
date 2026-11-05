using Mustask.Application.Abstractions.UserDirectory;

namespace Mustask.Application.Features.Organizations.SearchAppointeeCandidates;

/// <summary>
/// <c>GET /api/organizations/rescue/appointee-candidates?text=</c> (feat-033 api-contract § 1.2; US-2 "Only users who have
/// signed in are found", "The search does not reveal membership"): the active users of the projection by name or e-mail. It
/// names no organization, so it says nothing of any membership (D-4); mod-014 owns the rules of the search itself, including its
/// own 400 for a text over 100 characters.
/// </summary>
public sealed class SearchAppointeeCandidatesHandler(IUserDirectory directory)
{
    public async Task<AppointeeCandidatesResponse> Handle(SearchAppointeeCandidatesQuery query, CancellationToken cancellationToken)
    {
        var result = await directory.SearchAsync(new UserSearch(query.Text ?? string.Empty, IncludeInactive: false), cancellationToken);

        return new AppointeeCandidatesResponse(
            result.Users.Select(user => new AppointeeCandidateResponse(user.Id, user.DisplayName, user.Email)).ToList(),
            result.MoreMatched);
    }
}
