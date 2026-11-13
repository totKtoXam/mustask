using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetSelectorOffers;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/selector-offers</c> (feat-043 US-2, US-8, BR-NV-17): the departments, active projects
/// with their place and active teams of the organization, with the links between them, in a bounded read of five statements; the
/// panel computes the fit and the «Остальные» group from it, and the lists «Проекты» and «Команды» show its rows. The caller is a
/// required argument of the read (BR-NV-15).
/// </summary>
public sealed class GetSelectorOffersHandler(INavigationReadModel navigation, IOrganizationAccess access, ICurrentUser currentUser)
{
    public async Task<SelectorOffersResponse> Handle(GetSelectorOffersQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        var caller = new CallerAccess(organizationId, currentUser.SubjectId, access.Role);
        var rows = await navigation.ReadSelectorOffersAsync(caller, cancellationToken);

        return new SelectorOffersResponse(
            rows.Departments.Select(row => new OfferDepartmentResponse(row.Id, row.Name, row.Color)).ToList(),
            rows.Projects.Select(row => new OfferProjectResponse(row.Id, row.Name, row.Key, row.DepartmentId, row.DepartmentName, row.TeamIds, row.Color)).ToList(),
            rows.Teams.Select(row => new OfferTeamResponse(row.Id, row.Name, row.IsMember, row.DepartmentId, row.Color)).ToList());
    }
}
