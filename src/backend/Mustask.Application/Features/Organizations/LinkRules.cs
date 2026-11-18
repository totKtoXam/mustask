using Mustask.Domain.Organizations;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;

namespace Mustask.Application.Features.Organizations;

/// <summary>
/// What an unlink needs (mod-019 BR-SA-15, OQ-D2): a project–team link needs both ends' link codes (it reassigns items), a
/// department–team link of mod-018 is retired by doc-016. The per-row <c>canUnlink</c> flag is computed from the set form; it never decides a request, the
/// unlink route does.
/// </summary>
internal static class LinkRules
{
    public static bool CanUnlinkProjectTeam(AccessSet set, Guid projectId, Guid teamId) =>
        set.Holds(PermissionCatalog.OrganizationsProjectLink, projectId) && set.Holds(PermissionCatalog.OrganizationsTeamLink, teamId);
}

/// <summary>
/// The optional <c>linkableFor=team|project|area</c> query of the three pickers (mod-019 OQ-D2, api-contract § 4): the answer keeps only
/// the objects the caller may link, so a picker never offers what the link route would refuse. An unknown value is a 400.
/// </summary>
internal static class LinkableFor
{
    /// <summary><c>true</c> when the filter is asked for; throws the 400 for a value that is not one of the three.</summary>
    public static bool IsRequested(string? value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is "team" or "project" or "area")
        {
            return true;
        }

        throw new Mustask.Shared.Exceptions.ValidationException(
            OrganizationErrorCodes.ValidationFailed,
            new Dictionary<string, IReadOnlyList<Mustask.Shared.Errors.FieldError>>
            {
                ["linkableFor"] = [new Mustask.Shared.Errors.FieldError(OrganizationErrorCodes.ValidationFailed)],
            });
    }
}

/// <summary>
/// doc-016 BR-TD-3 (mod-022): a project–team link joins a team and a project standing in the same department. Each project's node is
/// locked <c>FOR SHARE</c> in id order and its department read under that lock, so a link against a move of the project (which holds the
/// node <c>FOR NO KEY UPDATE</c>) ends in one order: the link refused, or the move refused (BR-TD-4).
/// </summary>
internal static class LinkPlaces
{
    public static async Task EnsureSameDepartmentAsync(
        IAreaRepository areas, Guid organizationId, IEnumerable<Guid> projectIds, Guid? departmentId, CancellationToken cancellationToken)
    {
        var wanted = projectIds.Distinct().Order().ToList();

        if (wanted.Count == 0)
        {
            return;
        }

        // One statement for all the nodes, whatever their number (the statement-count rule), in project-id order.
        var places = await areas.LockProjectNodesForShareAsync(organizationId, wanted, cancellationToken);

        if (wanted.Any(projectId => !places.ContainsKey(projectId)))
        {
            throw new Mustask.Shared.Exceptions.NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        if (departmentId is null || wanted.Any(projectId => places[projectId] != departmentId))
        {
            throw new Mustask.Shared.Exceptions.ConflictException(OrganizationErrorCodes.TeamOtherDepartment);
        }
    }
}
