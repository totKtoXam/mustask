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
