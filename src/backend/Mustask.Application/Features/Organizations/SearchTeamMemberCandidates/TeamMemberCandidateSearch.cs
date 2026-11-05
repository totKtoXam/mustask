using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;

namespace Mustask.Application.Features.Organizations.SearchTeamMemberCandidates;

/// <summary>
/// The one definition of the «Участник организации» search that both routes of this slice answer — a team's own
/// (<c>…/teams/{teamId}/member-candidates</c>) and the one of a team being created (<c>…/teams/member-candidates</c>): the
/// restriction to the organization's members with mod-014's <c>AmongIds</c>, split above 500 ids into batches and merged
/// (<see cref="UserDirectoryBatches"/>, BR-UP-17), mod-014's own rules for the text (trimmed, at least 2 characters, at most 20
/// answered with <c>moreMatched</c>, name or e-mail, letter case not deciding, a refusal of a text over 100 characters passed
/// through unchanged), the shape of <see cref="TeamMemberCandidatesResponse"/>.
/// </summary>
internal static class TeamMemberCandidateSearch
{
    /// <summary>
    /// Two statements for an organization of up to 500 members — the member ids, then one search of the projection — and one more
    /// per further 500. A user disabled in Keycloak, a projected user in no organization and a member of another one are not offered.
    /// </summary>
    public static async Task<TeamMemberCandidatesResponse> SearchAsync(
        IMemberReadModel members,
        IUserDirectory directory,
        Guid organizationId,
        string? text,
        CancellationToken cancellationToken)
    {
        var memberIds = await members.ListMemberIdsAsync(organizationId, cancellationToken);
        var result = await UserDirectoryBatches.SearchAmongAsync(directory, text ?? string.Empty, memberIds, cancellationToken);

        return new TeamMemberCandidatesResponse(
            result.Users.Select(user => new TeamMemberCandidateResponse(user.Id, user.DisplayName, user.Email)).ToList(),
            result.MoreMatched);
    }
}
