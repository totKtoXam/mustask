using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Abstractions.WorkItems;

/// <summary>
/// One parent write as it arrives (feat-001 api-contract § 2.3), shared by the
/// two routes that write a parent — the parent write itself and the form's save.
/// Both values are the untrusted strings they arrived as.
/// </summary>
/// <param name="ParentId">The new parent's id, or <c>null</c> to clear.</param>
/// <param name="SeenParentId">The parent the user saw, or <c>null</c> when they saw none.</param>
public sealed record ParentChange(string? ParentId, string? SeenParentId)
{
    /// <summary>
    /// The new parent: none, or its id. A value in any other form names nothing,
    /// which is answered as a parent that is not an item of the project (BR-H-1).
    /// </summary>
    /// <exception cref="NotFoundException"><see cref="WorkItemErrorCodes.ParentNotFound"/>.</exception>
    public Guid? NewParentId()
    {
        if (ParentId is null)
        {
            return null;
        }

        return AddressedId.TryParse(ParentId, out var id) ? id : throw new NotFoundException(WorkItemErrorCodes.ParentNotFound);
    }

    /// <summary>
    /// The parent seen: none, or its id; <see cref="Guid.Empty"/> for a value in
    /// any other form, which names nothing and so never matches (BR-H-4).
    /// </summary>
    public Guid? SeenId()
    {
        if (SeenParentId is null)
        {
            return null;
        }

        return AddressedId.TryParse(SeenParentId, out var id) ? id : Guid.Empty;
    }
}

/// <summary>The organization's process as the hierarchy's rules read it (BR-H-2, BR-H-8).</summary>
public static class ProcessHierarchy
{
    public static HierarchyTypes Of(ProcessDefinitionRow process)
    {
        ArgumentNullException.ThrowIfNull(process);

        var ranks = process.BacklogLevels.ToDictionary(level => level.Id, level => level.Rank);

        return new HierarchyTypes(process.WorkItemTypes.Select(type => new HierarchyType(
            type.Id,
            type.Key,
            ranks[type.BacklogLevelId],
            type.States.Where(state => state.Category == StateCategory.Removed).Select(state => state.Id).ToList())));
    }
}
