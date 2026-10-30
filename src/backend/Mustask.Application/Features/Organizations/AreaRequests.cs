using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations;

/// <summary>
/// The small parts of an area request that several slices share (mod-018): the ids of a body as a set, the tree of the rows a read
/// has, a node's type as it goes on the wire and the deactivated-project rule of a write. It sits at the module root, like
/// <see cref="IOrganizationStructure"/>, so no slice references another (docs/conventions/backend.md). It decides nothing about
/// access or order: every slice states its own order of answers.
/// </summary>
public static class AreaRequests
{
    /// <summary>
    /// The ids a body names, as a set: duplicates collapse, an absent list is empty. An entry that is not an id names nothing and
    /// answers <paramref name="notFoundCode"/> (a malformed id is the same 404 as an unknown one, BR-AW-1).
    /// </summary>
    public static HashSet<Guid> ParseIds(IReadOnlyList<string?>? raw, string notFoundCode)
    {
        var ids = new HashSet<Guid>();

        foreach (var entry in raw ?? [])
        {
            if (!AddressedId.TryParse(entry, out var id))
            {
                throw new NotFoundException(notFoundCode);
            }

            ids.Add(id);
        }

        return ids;
    }

    /// <summary>The tree of the organization's rows: pre-order, levels and paths come from it.</summary>
    public static AreaTree TreeOf(IReadOnlyList<AreaTreeRow> rows) => new(rows.Select(row => row.ToNode()));

    /// <summary>A node's type on the wire: <c>department</c>, <c>project</c> or <c>area</c> (api-contract § 1).</summary>
    public static string TypeName(AreaType type) =>
        type switch
        {
            AreaType.Department => "department",
            AreaType.Project => "project",
            _ => "area",
        };

    /// <summary>
    /// BR-AW-10, OQ-6 A: while its project is deactivated, a project's node and its sub-areas are read-only — a write on one answers
    /// <see cref="OrganizationErrorCodes.ProjectNotFound"/>, as everything under a deactivated project does (mod-002 BR-M1-20). Read
    /// after the node's own lock, as a new statement (ADR-0026 rule 8: a write beneath only reads its containers' status).
    /// </summary>
    public static async Task EnsureProjectActiveAsync(
        IProjectRepository projects, Guid organizationId, Area area, CancellationToken cancellationToken)
    {
        if (area.ProjectId is { } projectId && !await projects.IsReachableAsync(organizationId, projectId, cancellationToken))
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }
    }
}
