using Mustask.Domain.Process;

namespace Mustask.Domain.Boards;

/// <summary>A type of the level as the board needs it: its id and its states in the process's order (mod-001 BR-M2-2).</summary>
public sealed record LayoutType(Guid TypeId, IReadOnlyList<LayoutState> States);

/// <summary>A state of a type: its id, Russian name, category and 1-based position in its type.</summary>
public sealed record LayoutState(Guid StateId, string Name, StateCategory Category, int Position);

/// <summary>What a drop of a card of <see cref="TypeId"/> in a column writes: a state, or <c>null</c> for no drop target (BR-M4-18).</summary>
public sealed record ColumnTarget(Guid TypeId, Guid? StateId);

/// <summary>
/// One column of a board (BR-M4-2): the <see cref="Index"/>-th of its
/// <see cref="Category"/>, its header, and per type of the level, in display
/// order, the state it maps.
/// </summary>
public sealed record BoardColumn(StateCategory Category, int Index, string Header, IReadOnlyList<ColumnTarget> Targets)
{
    /// <summary>The column's stable key (api-contract § 3.3): the category's code and the 1-based index, <c>InProgress-2</c>.</summary>
    public string Key => $"{Category}-{Index}";

    /// <summary>The state this column maps for <paramref name="typeId"/>, or <c>null</c>.</summary>
    public Guid? StateFor(Guid typeId) => Targets.SingleOrDefault(target => target.TypeId == typeId)?.StateId;
}

/// <summary>
/// The columns of a level's board, derived from the process on every read and
/// never stored (spec § The board's columns, doc-003 BR-M4-2): the categories in
/// the order «Новые» → «В работе» → «Решённые» → «Завершённые», Removed having
/// none; within a category as many columns as the most states any type of the
/// level has there, the k-th mapping each type to its k-th state of that
/// category in the process's order, or to none. The first column of a category
/// is headed by the category's label, a further one by the name of the state it
/// maps for the first type, in display order, that maps one.
/// </summary>
public sealed class BoardLayout
{
    /// <summary>The categories that have columns, in board order.</summary>
    public static readonly IReadOnlyList<StateCategory> CategoryOrder =
        [StateCategory.Proposed, StateCategory.InProgress, StateCategory.Resolved, StateCategory.Completed];

    private readonly Dictionary<(Guid TypeId, Guid StateId), int> _columnOfState = [];

    private BoardLayout(IReadOnlyList<BoardColumn> columns)
    {
        Columns = columns;

        for (var index = 0; index < columns.Count; index++)
        {
            foreach (var target in columns[index].Targets)
            {
                if (target.StateId is { } stateId)
                {
                    _columnOfState[(target.TypeId, stateId)] = index;
                }
            }
        }
    }

    public IReadOnlyList<BoardColumn> Columns { get; }

    /// <param name="types">The level's types in the process's display order.</param>
    public static BoardLayout Derive(IReadOnlyList<LayoutType> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        var columns = new List<BoardColumn>();

        foreach (var category in CategoryOrder)
        {
            var statesOfType = types
                .Select(type => (type.TypeId, States: type.States.Where(state => state.Category == category).OrderBy(state => state.Position).ToList()))
                .ToList();

            var count = statesOfType.Count == 0 ? 0 : statesOfType.Max(type => type.States.Count);

            for (var k = 0; k < count; k++)
            {
                var targets = statesOfType
                    .Select(type => new ColumnTarget(type.TypeId, k < type.States.Count ? type.States[k].StateId : null))
                    .ToList();

                var header = k == 0
                    ? StateCategoryLabels.Of(category)
                    : statesOfType.First(type => k < type.States.Count).States[k].Name;

                columns.Add(new BoardColumn(category, k + 1, header, targets));
            }
        }

        return new BoardLayout(columns);
    }

    /// <summary>The index in <see cref="Columns"/> of the column an item of that type in that state sits in; <c>null</c> for a state with no column (Removed, or not the type's).</summary>
    public int? ColumnOf(Guid typeId, Guid stateId) =>
        _columnOfState.TryGetValue((typeId, stateId), out var index) ? index : null;
}
