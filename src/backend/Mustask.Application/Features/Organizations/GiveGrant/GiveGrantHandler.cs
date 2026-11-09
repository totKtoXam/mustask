using Microsoft.Extensions.Logging;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GiveGrant;

/// <summary>
/// Gives a member a role on a department, a project or a team (mod-019 api-contract § 2; US-1 "A manager gives a role of the
/// object's scope", "Only an active member of this organization can be given a role", "A grantee is never the grant's giver").
/// </summary>
/// <remarks>
/// <b>The order of answers</b>, one numbered step each: the address from the outermost level in (the object, locked
/// <c>FOR SHARE</c>); the rule about the object (a project node and a sub-area hold no grant, 409); the values of the body (400,
/// every reason at once) and the self-grant (400); the grantee (an active member of this organization, 404 otherwise); the
/// uniqueness pre-check (409 <c>grant.changed</c>); the save. <b>Locks</b>: the object <c>FOR SHARE</c> so it is not deleted under
/// the give; the composite key onto the organization membership takes <c>FOR KEY SHARE</c> on the insert, so a removal of that member
/// waits and then cascades the new row away, and one that committed first fails the insert (BR-SA-3). Nothing is granted by
/// belonging to a team and no team membership is read.
/// </remarks>
public sealed class GiveGrantHandler(
    IGrantRepository grants,
    IMemberReadModel members,
    IUserDirectory directory,
    ICurrentUser currentUser,
    IClock clock,
    IUnitOfWork unitOfWork,
    ILogger<GiveGrantHandler> logger)
{
    public async Task<GrantRowView> Handle(GiveGrantCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ObjectId, out var objectId))
                {
                    throw new NotFoundException(GrantObjects.NotFoundCodeOf(command.Scope));
                }

                // 2. The rule about the addressed object.
                switch (await grants.ResolveAsync(command.Scope, organizationId, objectId, lockRow: true, ct))
                {
                    case GrantObjectState.NotFound:
                        throw new NotFoundException(GrantObjects.NotFoundCodeOf(command.Scope));
                    case GrantObjectState.ScopeNotAllowed:
                        throw new ConflictException(OrganizationErrorCodes.GrantScopeNotAllowed);
                }

                // 3. The values of the body, and the self-grant.
                var actor = currentUser.SubjectId;
                var (userId, role) = GrantInput.Validate(command.UserId, command.Role, actor);

                // 4. The grantee: an active member of this organization, known to the projection (BR-SA-2, BR-M1-32).
                var lookup = await directory.LookupAsync([userId], ct);

                if (!lookup.Known.TryGetValue(userId, out var user)
                    || !user.IsActive
                    || !await members.IsMemberAsync(organizationId, userId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.MembershipNotFound);
                }

                // 5. One grant per member and object: giving twice is a change somebody else already made.
                if (await grants.FindAsync(command.Scope, objectId, userId, ct) is not null)
                {
                    throw new ConflictException(OrganizationErrorCodes.GrantChanged);
                }

                // 6. Save. The primary key closes a concurrent give, the membership key a concurrent removal.
                var now = clock.UtcNow;
                await grants.AddAsync(command.Scope, organizationId, objectId, userId, role, actor, now, ct);

                var giver = await directory.LookupAsync([actor], ct);
                var known = new Dictionary<string, DirectoryUser>(lookup.Known, StringComparer.Ordinal);

                foreach (var entry in giver.Known)
                {
                    known[entry.Key] = entry.Value;
                }

                logger.LogInformation(
                    "Grant given: {Scope} {ObjectId} in organization {OrganizationId}, user {UserId} as {Role}, by {ActorId}.",
                    command.Scope, objectId, organizationId, userId, role, actor);

                return GrantRows.Of(new GrantRecord(userId, role, actor, now), GrantOrigins.Held, null, known);
            },
            cancellationToken);
}
