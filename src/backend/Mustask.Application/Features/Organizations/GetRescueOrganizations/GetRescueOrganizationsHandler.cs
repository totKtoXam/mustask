using System.Globalization;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetRescueOrganizations;

/// <summary>
/// <c>GET /api/organizations/rescue</c> (feat-033 api-contract § 1.1; US-1): every organization of the instance, active or
/// deactivated, 25 to a page in name order, with the moment of its newest appointment. The instance policy has answered 401 and
/// 403. The page is validated; the name filter never is — trimmed, empty = none, a text that matches nothing is an empty answer.
/// </summary>
public sealed class GetRescueOrganizationsHandler(IAppointmentReadModel appointments)
{
    public async Task<RescueOrganizationsResponse> Handle(GetRescueOrganizationsQuery query, CancellationToken cancellationToken)
    {
        var page = 1;

        if (query.Page is not null
            && (!int.TryParse(query.Page, NumberStyles.None, CultureInfo.InvariantCulture, out page) || page < 1))
        {
            throw new ValidationException(
                OrganizationErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>> { ["page"] = [new FieldError(OrganizationErrorCodes.RescueInvalidPage)] });
        }

        var name = query.Name?.Trim();
        var result = await appointments.GetOrganizationsPageAsync(
            string.IsNullOrEmpty(name) ? null : name, page, OrganizationLimits.RescueListPageSize, cancellationToken);

        return new RescueOrganizationsResponse(
            result.Count,
            result.Rows
                .Select(row => new RescueOrganizationRow(
                    row.Id, row.Name, row.IsActive ? "active" : "deactivated", row.LastAppointedAt))
                .ToList());
    }
}
