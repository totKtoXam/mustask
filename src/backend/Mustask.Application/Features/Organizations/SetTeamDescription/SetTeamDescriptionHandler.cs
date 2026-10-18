using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SetTeamDescription;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/teams/{teamId}/description</c> (feat-028 api-contract § 2.1; US-1 "rename and
/// description", BR-OL-3): a team's description is set, kept exactly as sent, or cleared.
/// </summary>
/// <remarks>
/// <b>The order of answers</b> (api-contract § 0.5): the address, the team included (404); the value (400 <c>description</c>);
/// the save. <b>Locks</b> (plan-backend § 7.2): the team <c>FOR NO KEY UPDATE</c>, re-checked as active, exactly as a rename takes it,
/// so a deletion and a description serialize and the second answers <see cref="OrganizationErrorCodes.TeamNotFound"/>.
/// </remarks>
public sealed class SetTeamDescriptionHandler(ITeamRepository teams, IUnitOfWork unitOfWork)
{
    public async Task<SetTeamDescriptionResult> Handle(SetTeamDescriptionCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                var team = AddressedId.TryParse(command.TeamId, out var teamId)
                    ? await teams.LockForNoKeyUpdateAsync(organizationId, teamId, ct)
                    : null;

                if (team is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // 2. The value in the body: kept as typed; the empty string is none; over 2 000 characters is refused.
                if (!EntityDescription.TryCreate(command.Description, out var description))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["description"] = [EntityDescription.LengthReason] });
                }

                team.Describe(description);
                await teams.SaveAsync(ct);

                return new SetTeamDescriptionResult(new TeamResponse(team.Id, team.Name, team.Description));
            },
            cancellationToken);
}
