namespace Mustask.Domain.Process;

/// <summary>
/// The aggregate root of an organization's process (doc-007 M2): its backlog
/// levels, its work item types and, through the types, their states. A process
/// is per-organization data: it is created once, by <see cref="Seed"/>, when an
/// organization is created, and the MVP never changes it (BR-M2-13).
/// </summary>
/// <remarks>
/// The class is called <c>ProcessDefinition</c>, not <c>Process</c>: inside the
/// <c>Process</c> namespaces a bare <c>Process</c> binds to the namespace.
/// </remarks>
public sealed class ProcessDefinition
{
    private readonly List<BacklogLevel> _backlogLevels = [];
    private readonly List<WorkItemType> _workItemTypes = [];

    public ProcessId Id { get; private set; }

    /// <summary>
    /// The organization's identifier, an opaque seed key (doc-007 R-5). It is
    /// unique: the storage guard of the seed's idempotency (BR-M2-9).
    /// </summary>
    public Guid OrganizationId { get; private set; }

    public IReadOnlyList<BacklogLevel> BacklogLevels => _backlogLevels;

    public IReadOnlyList<WorkItemType> WorkItemTypes => _workItemTypes;

    private ProcessDefinition()
    {
        // EF materialization.
    }

    private ProcessDefinition(ProcessId id, Guid organizationId)
    {
        Id = id;
        OrganizationId = organizationId;
    }

    /// <summary>
    /// Builds the complete seeded process of <see cref="SeededProcess"/> with
    /// fresh identities, none of them shared with any other process (BR-M2-1,
    /// BR-M2-9). The only place the seed's input is checked.
    /// </summary>
    /// <exception cref="ArgumentException">The identifier is not well formed (<see cref="OrganizationKey"/>).</exception>
    /// <exception cref="InvalidOperationException">The blueprint breaks one of its invariants.</exception>
    public static ProcessDefinition Seed(ProcessId id, Guid organizationId)
    {
        if (!OrganizationKey.IsWellFormed(organizationId))
        {
            throw new ArgumentException("The organization identifier is not well formed.", nameof(organizationId));
        }

        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("The process identity must not be empty.", nameof(id));
        }

        var blueprint = SeededProcess.Blueprint;
        blueprint.EnsureValid();

        var process = new ProcessDefinition(id, organizationId);

        var levels = new Dictionary<LevelBlueprint, BacklogLevel>();

        foreach (var level in blueprint.Levels)
        {
            var created = new BacklogLevel(Guid.CreateVersion7(), id, level.Name, level.Rank, level.Kind);
            levels.Add(level, created);
            process._backlogLevels.Add(created);
        }

        var typePosition = 1;

        foreach (var type in blueprint.Types)
        {
            var created = new WorkItemType(
                Guid.CreateVersion7(), id, levels[type.Level].Id, type.Key, type.Name, type.Appearance, typePosition++);

            var statePosition = StateDefinition.InitialPosition;

            foreach (var state in type.States)
            {
                created.AddState(state.Name, state.Category, statePosition++);
            }

            process._workItemTypes.Add(created);
        }

        process.EnsureStateNamesHaveOneCategory();

        return process;
    }

    /// <summary>
    /// Refuses a process in which a state name stands in more than one category on its types (bug-004 D-3,
    /// <see cref="StateNameRule"/>). <see cref="Seed"/> is the only code that creates a state through the aggregate
    /// today and calls it; a write path that creates or renames a state later must call it before it saves.
    /// </summary>
    /// <exception cref="InvalidOperationException">A state name stands in more than one category.</exception>
    public void EnsureStateNamesHaveOneCategory() =>
        StateNameRule.Require(_workItemTypes.SelectMany(type => type.States).Select(state => (state.Name, state.Category)));
}
