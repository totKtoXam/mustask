using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Process;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.CreateOrganization;

/// <summary>
/// <c>POST /api/organizations</c> (US-1: an organization is created with its
/// process, by a user holding the create right). The one use case that calls
/// another module's seam, mod-001's <see cref="IProcessSeed"/>
/// (plan-backend § 10).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, § 10), one numbered step
/// each below. The route has no address and addresses no object, so the
/// address and the rules about the addressed object do not apply: the value
/// in the body, the uniqueness pre-check, the seed, the save. The create right
/// is not a step of the handler: the endpoint's policy answers it before the
/// handler runs (§ 7.4), and the handler never re-checks it.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): none. An insert is closed by the unique
/// index on the normalized name, which is also what makes the pre-check
/// advisory (§ 6.3).
/// </para>
/// <para>
/// <b>The seed</b> (mod-001 api-contract § 2, the caller's obligations): it runs
/// inside the unit of work, before the handler tracks any change of its own
/// (the pre-check is <c>AsNoTracking</c>, so it tracks nothing), and any failure
/// of it leaves the unit of work, so the transaction rolls back and the seeded
/// rows go with it (BR-M1-3). A lost name race rolls back the same way:
/// "the refused creation leaves no seeded process behind" (US-1).
/// </para>
/// <para>
/// <b>The founder</b> (mod-016 BR-M1-25; US-6, US-3 "A creator the projection does not know yet still gets the creation
/// memberships"): in the same unit of work, the same flush, the creator becomes the organization's <c>admin</c> — added
/// by themselves at the creation time — whatever the user projection says of them; a failed seed or a refused name leaves
/// no membership either. They administer at once, because access is read from the membership alone (BR-M1-32).
/// </para>
/// </remarks>
public sealed class CreateOrganizationHandler(
    IOrganizationRepository organizations,
    IMembershipRepository memberships,
    IProcessSeed processSeed,
    IClock clock,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<CreateOrganizationResult> Handle(CreateOrganizationCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address and 2. rules about the addressed object alone: none, the route is the collection's.

                // 3. The value in the body: one ValidationException with every field reason.
                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = [EntityName.LengthReason] });
                }

                // 4. References in the body: none.

                // 5. The uniqueness pre-check (BR-M1-1): any status holds the name, and the answer names the holder's
                // stored name, so it reads the same for an active and a deactivated one (US-1).
                var holder = await organizations.FindNameHolderAsync(name.Normalized, exceptId: null, ct);

                if (holder is not null)
                {
                    throw new ConflictException(
                        OrganizationErrorCodes.OrganizationNameTaken,
                        new ErrorParams { ["name"] = ErrorParam.Text(holder) });
                }

                // 6. The seed, before anything the handler tracks (mod-001's obligation 2). The id is made inside the
                // action, so an execution-strategy retry seeds a fresh one.
                var organizationId = Guid.CreateVersion7();
                var processId = await SeedAsync(organizationId, ct);

                // 7. Save. A concurrent creation of the same name that committed first fails this insert on the name
                // index, and the repository answers it with the pre-check's code (plan-backend § 6.3).
                var organization = Organization.Create(organizationId, name, processId, clock.UtcNow, currentUser.SubjectId);
                organizations.Add(organization);
                memberships.Add(OrganizationMembership.Founder(organization.Id, currentUser.SubjectId, organization.CreatedAt));
                await organizations.SaveAsync(ct);

                return new CreateOrganizationResult(new OrganizationResponse(organization.Id, organization.Name));
            },
            cancellationToken);

    /// <summary>
    /// Any failure of the seed but a cancellation is the server's, whatever its type: it becomes
    /// <see cref="OrganizationCreateFailedException"/>, HTTP 500, with the original as the inner exception, so it
    /// reaches the log and never the client (US-1 "A failed process seed leaves nothing behind", status.md D-t).
    /// </summary>
    private async Task<ProcessId> SeedAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        try
        {
            return await processSeed.SeedAsync(organizationId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new OrganizationCreateFailedException(exception);
        }
    }
}
