using Microsoft.Extensions.Logging;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.ChangeGrantRole;

/// <summary>
/// Changes the role of a grant (mod-019 api-contract § 2; US-1 "A role is changed, then removed", "A grantee is never the grant's
/// giver"). The role is replaced, and the grant records the changer and the time (BR-SA-1). Order of answers as
/// <c>GiveGrant</c>, then the grant itself: a grant that is gone, or changed under this write (<c>xmin</c>), answers 409
/// <c>grant.changed</c>. A change to the role it already has is a success that keeps the giver and the time.
/// </summary>
public sealed class ChangeGrantRoleHandler(
    IGrantRepository grants,
    IUserDirectory directory,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork,
    ILogger<ChangeGrantRoleHandler> logger)
{
    public async Task<GrantRowView> Handle(ChangeGrantRoleCommand command, CancellationToken cancellationToken) =>
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

                var actor = currentUser.SubjectId;
                var (userId, role) = GrantInput.Validate(command.UserId, command.Role, actor);

                var current = await grants.FindAsync(command.Scope, objectId, userId, ct)
                    ?? throw new ConflictException(OrganizationErrorCodes.GrantChanged);

                var now = clock.UtcNow;
                var result = current;

                if (current.Role != role)
                {
                    if (!await grants.ChangeRoleAsync(command.Scope, objectId, userId, role, actor, now, ct))
                    {
                        throw new ConflictException(OrganizationErrorCodes.GrantChanged);
                    }

                    result = new GrantRecord(userId, role, actor, now);

                    logger.LogInformation(
                        "Grant changed: {Scope} {ObjectId} in organization {OrganizationId}, user {UserId} now {Role}, by {ActorId}.",
                        command.Scope, objectId, organizationId, userId, role, actor);
                }

                var lookup = await UserDirectoryBatches.LookupAllAsync(directory, [result.UserId, result.GrantedBy], ct);


                return GrantRows.Of(result, GrantOrigins.Held, null, lookup.Known);
            },
            cancellationToken);
}
