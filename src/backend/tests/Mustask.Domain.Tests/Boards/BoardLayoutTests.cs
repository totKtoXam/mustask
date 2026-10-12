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

    // bug-004 D-3 · US-4 · "Each level's board has its own columns": the three levels have four, headed by the names of
    // the states they map («Новая», «Активна», «Разрешена», «Закрыта»), not by the categories' labels, in board order.
    [Theory]
    [InlineData("Эпики", "Новая,Активна,Разрешена,Закрыта")]
    [InlineData("Фичи", "Новая,Активна,Разрешена,Закрыта")]
    [InlineData("Истории", "Новая,Активна,Разрешена,Закрыта")]
    public void Derive_SeededProcess_HasTheSpecsColumns(string level, string headers)
    {
        var layout = BoardLayout.Derive(Seeded(level).Types);

        Assert.Equal(headers.Split(','), layout.Columns.Select(column => column.Header));
        Assert.Equal(
            ["Proposed-1", "InProgress-1", "Resolved-1", "Completed-1"],
            layout.Columns.Select(column => column.Key));
        Assert.DoesNotContain(
            layout.Columns,
            column => StateCategoryLabels.Of(column.Category) == column.Header);
    }

    // spec § Model's table for «Истории»: what a drop writes per type; every column maps both types (bug-004 D-3).
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
                "Новая: US:Новая BUG:Новая",
                "Активна: US:Активна BUG:Активна",
                "Разрешена: US:Разрешена BUG:Разрешена",
                "Закрыта: US:Закрыта BUG:Закрыта",
            ],
            layout.Columns.Select(column => $"{column.Header}: {Cell(column, us)} {Cell(column, bug)}"));
    }

    // US-4 · "A card sits in the column its type and state map to"; Removed («Удалена») has no column.
    [Theory]
    [InlineData("Истории", "US", "Новая", "Новая")]
    [InlineData("Истории", "BUG", "Активна", "Активна")]
    [InlineData("Истории", "US", "Разрешена", "Разрешена")]
    [InlineData("Истории", "BUG", "Разрешена", "Разрешена")]
    [InlineData("Истории", "BUG", "Закрыта", "Закрыта")]
    [InlineData("Эпики", "EPIC", "Разрешена", "Разрешена")]
    [InlineData("Фичи", "FEAT", "Разрешена", "Разрешена")]
    [InlineData("Истории", "US", "Удалена", null)]
    public void ColumnOf_SeededState_IsTheSpecsColumn(string level, string type, string state, string? column)
    {
        var (types, states) = Seeded(level);
        var layout = BoardLayout.Derive(types);

        var index = layout.ColumnOf(TypeIdOf(type), states[$"{type}:{state}"]);

        Assert.Equal(column, index is { } found ? layout.Columns[found].Header : null);
    }

    // bug-004 D-1 · the report: a story in «Разрешена» and a bug in «Разрешена» stood in different columns. Now they
    // stand in one — the same index, so one column on the board.
    [Fact]
    public void ColumnOf_AStoryAndABugInRazreshena_AreInOneColumn()
    {
        var (types, states) = Seeded("Истории");
        var layout = BoardLayout.Derive(types);

        var story = layout.ColumnOf(TypeIdOf("US"), states["US:Разрешена"]);
        var bug = layout.ColumnOf(TypeIdOf("BUG"), states["BUG:Разрешена"]);

        Assert.NotNull(story);
        Assert.Equal(story, bug);
        Assert.Single(layout.Columns, column => column.Header == "Разрешена");
    }

    // A process shaped differently, its types' names differing (a process of one's own is doc-007's, not built yet):
    // a column is headed by the state it maps for the first type, in display order, that maps one — the first type's
    // «Черновик», not the alphabetically first «Новая»; a category no type has gets no column; types map their k-th
    // state of the category, so a further column is headed by the only type that has it.
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

        Assert.Equal(["Черновик", "Ревью", "Тест", "Принята", "Готово"], layout.Columns.Select(column => column.Header));
        Assert.Equal(["Proposed-1", "InProgress-1", "InProgress-2", "InProgress-3", "Completed-1"], layout.Columns.Select(column => column.Key));
        Assert.Equal((firstReview, secondReview), (layout.Columns[1].StateFor(first), layout.Columns[1].StateFor(second)));
        Assert.Equal(((Guid?)null, (Guid?)secondTest), (layout.Columns[2].StateFor(first), layout.Columns[2].StateFor(second)));
        Assert.DoesNotContain(layout.Columns, column => column.Category is StateCategory.Resolved or StateCategory.Removed);
    }

    // The tie rule at the first column of a category: the types' names differ, the first type in display order heads it,
    // whatever the second says, and both types' states stay mapped by that one column.
    [Fact]
    public void Derive_TypesNameTheirFirstStateDifferently_TheFirstTypesNameHeadsTheColumn()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var firstState = Guid.NewGuid();
        var secondState = Guid.NewGuid();

        var layout = BoardLayout.Derive(
        [
            new LayoutType(first, [new LayoutState(firstState, "Яблоко", StateCategory.Proposed, 1)]),
            new LayoutType(second, [new LayoutState(secondState, "Арбуз", StateCategory.Proposed, 1)]),
        ]);

        var column = Assert.Single(layout.Columns);
        Assert.Equal("Яблоко", column.Header);
        Assert.Equal((firstState, secondState), (column.StateFor(first), column.StateFor(second)));
    }

    // A category only one type has (as a task has no Resolved state): its column is headed by that type's state and maps
    // nothing for the other, which is then no drop target there (BR-M4-18).
    [Fact]
    public void Derive_ACategoryOnlyOneTypeHas_MapsTheOtherTypeToNone()
    {
        var withReview = Guid.NewGuid();
        var without = Guid.NewGuid();
        var review = Guid.NewGuid();

        var layout = BoardLayout.Derive(
        [
            new LayoutType(withReview,
            [
                new LayoutState(Guid.NewGuid(), "Новая", StateCategory.Proposed, 1),
                new LayoutState(review, "Проверка", StateCategory.Resolved, 2),
            ]),
            new LayoutType(without, [new LayoutState(Guid.NewGuid(), "Новая", StateCategory.Proposed, 1)]),
        ]);

        Assert.Equal(["Новая", "Проверка"], layout.Columns.Select(column => column.Header));
        Assert.Equal(((Guid?)review, (Guid?)null), (layout.Columns[1].StateFor(withReview), layout.Columns[1].StateFor(without)));
    }
}
