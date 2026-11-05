using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetProjectTeamLinks;

/// <summary>The query of <c>GET /api/organizations/{organizationId}/projects/{projectId}/team-links</c>.</summary>
public sealed record GetProjectTeamLinksQuery(string? OrganizationId, string? ProjectId);

/// <summary>One linked team as the project node's read-only list shows it (mod-018 api-contract § 4, <c>ProjectTeamLink</c>).</summary>
public sealed record ProjectTeamLinkResponse(Guid Id, string Name);

/// <summary>The handler's outcome: the linked active teams by name.</summary>
public sealed record GetProjectTeamLinksResult(IReadOnlyList<ProjectTeamLinkResponse> Items);

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/team-links</c> (mod-018 api-contract § 4, design § 4, OQ-6 A): the
/// active teams linked to a project of the organization, <b>whatever the project's status</b>. The project node's «Команды» section
/// reads it, so a deactivated project's node still shows its links, read-only; the section's writes stay on
/// <c>…/projects/{id}/teams</c> (feat-028), which answers 404 for a deactivated project. A project that is unknown or of another
/// organization is <see cref="OrganizationErrorCodes.ProjectNotFound"/>.
/// </summary>
public sealed class GetProjectTeamLinksHandler(IOrganizationStructure structure)
{
    public async Task<GetProjectTeamLinksResult> Handle(GetProjectTeamLinksQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await structure.FindProjectAsync(organizationId, projectId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        var teams = await structure.ListLinkedTeamsAsync(organizationId, projectId, cancellationToken);

        return new GetProjectTeamLinksResult(teams.Select(team => new ProjectTeamLinkResponse(team.Id, team.Name)).ToList());
    }
}
