using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.RevokeGrant;

/// <summary>
/// Removes a grant (mod-019 api-contract § 2; US-1 "A role is changed, then removed", "Edge cases of the managers themselves").
/// Anyone who holds the object's grant code may remove any grant, their own included (BR-SA-19 forbids giving or changing one's own
/// grant, never removing it). A grant that is not there answers 409 <c>grant.changed</c>: somebody else removed it first, and two
/// managers removing each other's grants at once may both succeed (BR-SA-2).
/// </summary>
public sealed class RevokeGrantHandler(IGrantRepository grants, IUnitOfWork unitOfWork)
{
    public async Task Handle(RevokeGrantCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ObjectId, out var objectId))
                {
                    throw new NotFoundException(GrantObjects.NotFoundCodeOf(command.Scope));
                }

                switch (await grants.ResolveAsync(command.Scope, organizationId, objectId, lockRow: true, ct))
                {
                    case GrantObjectState.NotFound:
                        throw new NotFoundException(GrantObjects.NotFoundCodeOf(command.Scope));
                    case GrantObjectState.ScopeNotAllowed:
                        throw new ConflictException(OrganizationErrorCodes.GrantScopeNotAllowed);
                }

                if (string.IsNullOrWhiteSpace(command.UserId)
                    || !await grants.RemoveAsync(command.Scope, objectId, command.UserId, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.GrantChanged);
                }

                return true;
            },
            cancellationToken);
}
