using Mustask.Domain.Boards;
using Mustask.Domain.Process;

namespace Mustask.Domain.Tests.Boards;

/// <summary>The board's columns (spec § The board's columns, BR-M4-2), for the seeded process and for one shaped differently.</summary>
public sealed class BoardLayoutTests
{
    /// <summary>The seeded types of one level, in display order, each state under a fresh id, named "KEY:state".</summary>
    private static (IReadOnlyList<LayoutType> Types, Dictionary<string, Guid> States) Seeded(string levelName)
    {
        var states = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var types = SeededProcess.Blueprint.Types
            .Where(type => type.Level.Name == levelName)
            .Select(type => new LayoutType(
                TypeIdOf(type.Key),
                type.States
                    .Select((state, index) =>
                    {
                        var id = Guid.NewGuid();
                        states[$"{type.Key}:{state.Name}"] = id;

                        return new LayoutState(id, state.Name, state.Category, index + 1);
                    })
                    .ToList()))
            .ToList();

        return (types, states);
    }

    private static readonly Dictionary<string, Guid> TypeIds = new(StringComparer.Ordinal);

    private static Guid TypeIdOf(string key)
    {
        if (!TypeIds.TryGetValue(key, out var id))
        {
            id = Guid.NewGuid();
            TypeIds[key] = id;
        }

        return id;
    }

    // US-4 · "Each level's board has its own columns": «Эпики» and «Фичи» have four, «Истории» five, in board order.
    [Theory]
    [InlineData("Эпики", "Новые,В работе,Разрешена,Завершённые")]
    [InlineData("Фичи", "Новые,В работе,Разрешена,Завершённые")]
    [InlineData("Истории", "Новые,В работе,Разрешена,Решённые,Завершённые")]
    public void Derive_SeededProcess_HasTheSpecsColumns(string level, string headers)
    {
        var layout = BoardLayout.Derive(Seeded(level).Types);

        Assert.Equal(headers.Split(','), layout.Columns.Select(column => column.Header));
    }

    // spec § Model's table for «Истории»: what a drop writes per type, "—" being no drop target (BR-M4-18).
    [Fact]
    public void Derive_SeededStories_MapsEachTypeAsTheSpecsTable()
    {
        var (types, states) = Seeded("Истории");
        var layout = BoardLayout.Derive(types);
        var us = TypeIdOf("US");
        var bug = TypeIdOf("BUG");

        string Cell(BoardColumn column, Guid type) =>
            column.StateFor(type) is { } state ? states.Single(pair => pair.Value == state).Key : "—";

        Assert.Equal(
            [
                "Новые: US:Новая BUG:Новая",
                "В работе: US:Активна BUG:Активна",
                "Разрешена: US:Разрешена —",
                "Решённые: — BUG:Разрешена",
                "Завершённые: US:Закрыта BUG:Закрыта",
            ],
            layout.Columns.Select(column => $"{column.Header}: {Cell(column, us)} {Cell(column, bug)}"));
        Assert.Equal(
            ["Proposed-1", "InProgress-1", "InProgress-2", "Resolved-1", "Completed-1"],
            layout.Columns.Select(column => column.Key));
    }

    // US-4 · "A card sits in the column its type and state map to"; Removed («Удалена») has no column.
    [Theory]
    [InlineData("Истории", "US", "Новая", "Новые")]
    [InlineData("Истории", "BUG", "Активна", "В работе")]
    [InlineData("Истории", "US", "Разрешена", "Разрешена")]
    [InlineData("Истории", "BUG", "Разрешена", "Решённые")]
    [InlineData("Истории", "BUG", "Закрыта", "Завершённые")]
    [InlineData("Эпики", "EPIC", "Разрешена", "Разрешена")]
    [InlineData("Истории", "US", "Удалена", null)]
    public void ColumnOf_SeededState_IsTheSpecsColumn(string level, string type, string state, string? column)
    {
        var (types, states) = Seeded(level);
        var layout = BoardLayout.Derive(types);

        var index = layout.ColumnOf(TypeIdOf(type), states[$"{type}:{state}"]);

        Assert.Equal(column, index is { } found ? layout.Columns[found].Header : null);
    }

    // A process shaped differently: a further column of a category is headed by the state it maps for the first type, in
    // display order, that maps one; a category no type has gets no column; types map their k-th state of the category.
    [Fact]
    public void Derive_DifferentProcess_ColumnsFollowTheMostStatesPerCategory()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var firstReview = Guid.NewGuid();
        var secondTest = Guid.NewGuid();
        var secondReview = Guid.NewGuid();

        var layout = BoardLayout.Derive(
        [
            new LayoutType(first,
            [
                new LayoutState(Guid.NewGuid(), "Черновик", StateCategory.Proposed, 1),
                new LayoutState(firstReview, "Ревью", StateCategory.InProgress, 2),
                new LayoutState(Guid.NewGuid(), "Готово", StateCategory.Completed, 3),
                new LayoutState(Guid.NewGuid(), "Удалена", StateCategory.Removed, 4),
            ]),
            new LayoutType(second,
            [
                new LayoutState(Guid.NewGuid(), "Новая", StateCategory.Proposed, 1),
                new LayoutState(secondTest, "Тест", StateCategory.InProgress, 3),
                new LayoutState(secondReview, "Ревью", StateCategory.InProgress, 2),
                new LayoutState(Guid.NewGuid(), "Принята", StateCategory.InProgress, 4),
                new LayoutState(Guid.NewGuid(), "Готово", StateCategory.Completed, 5),
                new LayoutState(Guid.NewGuid(), "Удалена", StateCategory.Removed, 6),
            ]),
        ]);

        Assert.Equal(["Новые", "В работе", "Тест", "Принята", "Завершённые"], layout.Columns.Select(column => column.Header));
        Assert.Equal((firstReview, secondReview), (layout.Columns[1].StateFor(first), layout.Columns[1].StateFor(second)));
        Assert.Equal(((Guid?)null, (Guid?)secondTest), (layout.Columns[2].StateFor(first), layout.Columns[2].StateFor(second)));
        Assert.DoesNotContain(layout.Columns, column => column.Category is StateCategory.Resolved or StateCategory.Removed);
    }
}
