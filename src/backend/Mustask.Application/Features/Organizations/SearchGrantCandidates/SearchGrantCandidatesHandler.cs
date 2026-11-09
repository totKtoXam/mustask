using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SearchGrantCandidates;

/// <summary>
/// The grantee picker of an object's grants (mod-019 api-contract § 2, OQ-D2, design § 3): active members of this organization
/// matching the text, <b>never the caller</b> (BR-SA-19) and <b>never someone who already holds a grant on this object</b> — the
/// role of a holder is changed in the list. Only the holders of the object's own grants are left out: a department grant that reaches
/// a project does not exclude its holder from the project's picker.
/// </summary>
public sealed class SearchGrantCandidatesHandler(
    IGrantRepository grants,
    IMemberReadModel members,
    IUserDirectory directory,
    ICurrentUser currentUser)
{
    public async Task<SearchGrantCandidatesResponse> Handle(SearchGrantCandidatesQuery query, CancellationToken cancellationToken)
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
                throw new ConflictException(OrganizationErrorCodes.GrantScopeNotAllowed);
        }

        var memberIds = await members.ListMemberIdsAsync(organizationId, cancellationToken);
        var holders = (await grants.ListAsync(query.Scope, objectId, cancellationToken)).Select(grant => grant.UserId).ToHashSet(StringComparer.Ordinal);
        var caller = currentUser.SubjectId;
        var eligible = memberIds.Where(id => id != caller && !holders.Contains(id)).ToList();

        var result = await UserDirectoryBatches.SearchAmongAsync(directory, query.Text ?? string.Empty, eligible, cancellationToken);

        return new SearchGrantCandidatesResponse(result.Users.Select(UserRefView.From).ToList(), result.MoreMatched);
    }
}
