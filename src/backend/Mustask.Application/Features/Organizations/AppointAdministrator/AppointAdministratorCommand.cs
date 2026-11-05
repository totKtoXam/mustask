namespace Mustask.Application.Features.Organizations.AppointAdministrator;

/// <summary>
/// <c>POST /api/organizations/rescue/{organizationId}/appointments</c> (feat-033 api-contract § 1.3). The id of the address and
/// the two fields of the body are the untrusted strings that arrived; the handler judges every one of them.
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="UserId">The user to appoint, as sent: 1 to 255 characters, not blank. Never trimmed.</param>
/// <param name="Reason">The «Причина», judged after trimming.</param>
public sealed record AppointAdministratorCommand(string? OrganizationId, string? UserId, string? Reason);
