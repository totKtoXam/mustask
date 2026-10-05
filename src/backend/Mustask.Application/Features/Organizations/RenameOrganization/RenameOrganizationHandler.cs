using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.RenameOrganization;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/name</c> (US-2: the organization is
/// renamed). Its projects, teams and areas do not change, and its own name in
/// another letter case is allowed (BR-M1-1).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4), one
/// numbered step each below: the address, which here is the organization alone
/// (the endpoint filter has answered for it already, and the handler re-checks
/// it because it writes that row); no rule about the addressed object alone; the
/// name in the body; no reference in the body; the uniqueness pre-check; the
/// save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the organization <c>FOR NO KEY UPDATE</c>,
/// taken before anything is decided, so a deactivation that committed since the
/// filter answered is seen by the re-check of <c>Active</c> (ADR-0026 rule 5).
/// Two renames to one free name lock two different rows and do not meet there:
/// the unique index on the normalized name closes that race, and the repository
/// answers the loser's save with the pre-check's code (plan-backend § 6.3, § 8.1).
/// </para>
/// </remarks>
public sealed class RenameOrganizationHandler(IOrganizationRepository organizations, IUnitOfWork unitOfWork)
{
    public async Task<RenameOrganizationResult> Handle(RenameOrganizationCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address. A malformed id, an unknown one and a deactivated organization all answer the same
                // not-found (BR-M1-20); a deactivated organization takes no change (US-2).
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                var organization = await organizations.LockAsync(organizationId, ct);

                if (organization is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                // 2. Rules about the addressed object alone: none, an organization may always be renamed.

                // 3. Values in the body: one ValidationException with every field reason.
                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = [EntityName.LengthReason] });
                }

                // 4. References in the body: none.

                // 5. The uniqueness pre-check (BR-M1-1): instance-wide, an organization of any status holds its name, and the
                // answer names the holder's stored name. Excluding this organization is what lets it take its own name in
                // another letter case.
                var holder = await organizations.FindNameHolderAsync(name.Normalized, organization.Id, ct);

                if (holder is not null)
                {
                    throw new ConflictException(
                        OrganizationErrorCodes.OrganizationNameTaken,
                        new ErrorParams { ["name"] = ErrorParam.Text(holder) });
                }

                organization.Rename(name);

                // 6. Save. A concurrent rename or creation to the same name that committed first fails this update on the
                // name index, and the repository answers it with the pre-check's code, naming the submitted name
                // (plan-backend § 6.3).
                await organizations.SaveAsync(ct);

                return new RenameOrganizationResult(new OrganizationResponse(organization.Id, organization.Name));
            },
            cancellationToken);
}
