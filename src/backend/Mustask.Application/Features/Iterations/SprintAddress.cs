using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Iterations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Iterations;

/// <summary>
/// The address of a sprint route, judged from the outside in (api-contract § 0): the organization, the team (unknown, foreign and
/// deleted alike), the sprint. Shared by the module's slices as a plain helper; a slice never references another.
/// </summary>
internal static class SprintAddress
{
    public static Guid Organization(string? organizationId) =>
        AddressedId.TryParse(organizationId, out var id) ? id : throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);

    /// <summary>The team of the address, read: reachable (the team and its organization active), else 404.</summary>
    public static async Task<Guid> TeamAsync(
        IOrganizationStructure structure, Guid organizationId, string? teamId, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(teamId, out var id)
            || await structure.FindTeamAsync(organizationId, id, cancellationToken) is not { IsReachable: true })
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        return id;
    }

    /// <summary>
    /// The team of the address bound for a write: locked <c>FOR SHARE</c> and re-checked active (ADR-0028 rule 8), so a team
    /// deleted meanwhile answers its not-found and its deletion waits for this write.
    /// </summary>
    public static async Task LockTeamAsync(ITeamRepository teams, Guid organizationId, Guid teamId, CancellationToken cancellationToken)
    {
        if (await teams.LockForShareAsync(organizationId, teamId, cancellationToken) is not { IsActive: true })
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }
    }

    public static Guid Sprint(string? sprintId) =>
        AddressedId.TryParse(sprintId, out var id) ? id : throw new NotFoundException(SprintErrorCodes.NotFound);

    public static NotFoundException SprintNotFound() => new(SprintErrorCodes.NotFound);

    /// <summary>
    /// The body's name and dates, every refusal at once (US-1, BR-4, BR-7). A missing or empty name is acceptable only where
    /// <paramref name="nameOptional"/> (the creation takes the default).
    /// </summary>
    public static (SprintName? Name, SprintDates Dates) Body(string? name, DateOnly? startDate, DateOnly? endDate, bool nameOptional)
    {
        var errors = new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal);
        SprintName? parsed = null;

        if (nameOptional && string.IsNullOrWhiteSpace(name))
        {
            parsed = null;
        }
        else if (SprintName.TryCreate(name, out var created))
        {
            parsed = created;
        }
        else
        {
            errors["name"] = [SprintName.InvalidReason];
        }

        var dates = new SprintDates(startDate, endDate);

        if (dates.Check() is { } reason)
        {
            errors["endDate"] = [new FieldError(reason)];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(SprintErrorCodes.ValidationFailed, errors);
        }

        return (parsed, dates);
    }
}
