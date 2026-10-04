using Mustask.Domain.Process;

namespace Mustask.Domain.Tests.Process;

/// <summary>
/// US-1 (mod-001-process-seed): the seeded process has the content of doc-007
/// § "MVP: the seeded process" and plan-backend.md § 3, and every identity in
/// it is its own.
/// </summary>
public sealed class SeededProcessTests
{
    private static readonly Guid OrganizationId = Guid.Parse("a1b2c3d4-0000-4000-8000-000000000001");

    private static ProcessDefinition Seed(Guid? organizationId = null) =>
        ProcessDefinition.Seed(ProcessId.New(), organizationId ?? OrganizationId);

    private static WorkItemType Type(ProcessDefinition process, string key) =>
        process.WorkItemTypes.Single(type => type.Key == key);

    private static string StatesOf(WorkItemType type) =>
        string.Join("; ", type.States.Select(state => $"{state.Position} {state.Name} {state.Category}"));

    // US-1 · "A new organization identifier receives the complete seeded process": one process, 4 levels, 5 types, 24 states.
    [Fact]
    public void Seed_NewOrganization_HasFourLevelsFiveTypesAndTwentyFourStates()
    {
        var process = Seed();

        Assert.Equal(OrganizationId, process.OrganizationId);
        Assert.Equal(4, process.BacklogLevels.Count);
        Assert.Equal(5, process.WorkItemTypes.Count);
        Assert.Equal(24, process.WorkItemTypes.Sum(type => type.States.Count));
    }

    // US-1 · "...exactly the five types ... with the keys EPIC, FEAT, US, TASK and BUG" (BR-M2-9).
    [Fact]
    public void Seed_NewOrganization_HasTheFiveTypesWithTheirKeysAndNames()
    {
        var process = Seed();

        var types = process.WorkItemTypes.OrderBy(type => type.Position).Select(type => (type.Key, type.Name)).ToList();

        Assert.Equal(
            [
                ("EPIC", "Эпик"),
                ("FEAT", "Фича"),
                ("US", "Пользовательская история"),
                ("BUG", "Баг"),
                ("TASK", "Задача"),
            ],
            types);
    }

    // US-1 · "...each type has exactly the states doc-007 lists for it, in that order, each in the category stated there".
    [Theory]
    [InlineData("EPIC", "1 Новая Proposed; 2 Активна InProgress; 3 Разрешена InProgress; 4 Закрыта Completed; 5 Удалена Removed")]
    [InlineData("FEAT", "1 Новая Proposed; 2 Активна InProgress; 3 Разрешена InProgress; 4 Закрыта Completed; 5 Удалена Removed")]
    [InlineData("US", "1 Новая Proposed; 2 Активна InProgress; 3 Разрешена InProgress; 4 Закрыта Completed; 5 Удалена Removed")]
    [InlineData("BUG", "1 Новая Proposed; 2 Активна InProgress; 3 Разрешена Resolved; 4 Закрыта Completed; 5 Удалена Removed")]
    [InlineData("TASK", "1 Новая Proposed; 2 Активна InProgress; 3 Закрыта Completed; 4 Удалена Removed")]
    public void Seed_NewOrganization_EachTypeHasItsStatesInOrderWithTheirCategories(string key, string expected)
    {
        var process = Seed();

        Assert.Equal(expected, StatesOf(Type(process, key)));
    }

    // US-1 · "«Задача» has no state in the Resolved category".
    [Fact]
    public void Seed_NewOrganization_TaskHasNoResolvedState()
    {
        var process = Seed();

        Assert.DoesNotContain(Type(process, "TASK").States, state => state.Category == StateCategory.Resolved);
    }

    // US-1 · "...«Удалена» last and the first state as the type's initial state" (BR-M2-2).
    [Fact]
    public void Seed_NewOrganization_FirstStateIsInitialAndRemovedIsLastInEveryType()
    {
        var process = Seed();

        foreach (var type in process.WorkItemTypes)
        {
            var ordered = type.States.OrderBy(state => state.Position).ToList();

            Assert.Equal(StateDefinition.InitialPosition, ordered[0].Position);
            Assert.Equal("Новая", ordered[0].Name);
            Assert.Equal(StateCategory.Removed, ordered[^1].Category);
            Assert.Equal("Удалена", ordered[^1].Name);
            Assert.Equal(Enumerable.Range(1, ordered.Count), ordered.Select(state => state.Position));
        }
    }

    // US-1 · "...each level carries the rank and kind stated there" (BR-M2-4).
    [Fact]
    public void Seed_NewOrganization_HasTheFourLevelsWithNameRankAndKind()
    {
        var process = Seed();

        var levels = process.BacklogLevels.OrderByDescending(level => level.Rank).Select(level => (level.Name, level.Rank, level.Kind)).ToList();

        Assert.Equal(
            [
                ("Эпики", 4, BacklogLevelKind.Portfolio),
                ("Фичи", 3, BacklogLevelKind.Portfolio),
                ("Истории", 2, BacklogLevelKind.Requirement),
                ("Задачи", 1, BacklogLevelKind.Task),
            ],
            levels);
    }

    // US-1 · "...each type is on exactly one of the four backlog levels", story and bug on the same one.
    [Theory]
    [InlineData("EPIC", "Эпики")]
    [InlineData("FEAT", "Фичи")]
    [InlineData("US", "Истории")]
    [InlineData("BUG", "Истории")]
    [InlineData("TASK", "Задачи")]
    public void Seed_NewOrganization_EachTypeIsOnItsLevel(string key, string levelName)
    {
        var process = Seed();

        var level = process.BacklogLevels.Single(l => l.Id == Type(process, key).BacklogLevelId);

        Assert.Equal(levelName, level.Name);
    }

    // US-1 · "...each type carries exactly one appearance token set by the seed ... no two types share a token".
    [Fact]
    public void Seed_NewOrganization_EachTypeHasItsOwnAppearanceToken()
    {
        var process = Seed();

        Assert.Equal(AppearanceToken.Epic, Type(process, "EPIC").Appearance);
        Assert.Equal(AppearanceToken.Feature, Type(process, "FEAT").Appearance);
        Assert.Equal(AppearanceToken.Story, Type(process, "US").Appearance);
        Assert.Equal(AppearanceToken.Task, Type(process, "TASK").Appearance);
        Assert.Equal(AppearanceToken.Bug, Type(process, "BUG").Appearance);
        Assert.Equal(5, process.WorkItemTypes.Select(type => type.Appearance).Distinct().Count());
    }

    // US-1 · "...no two types, states or backlog levels share an identity, even where their labels are equal" (BR-M2-9).
    [Fact]
    public void Seed_NewOrganization_EveryIdentityIsDistinct_EvenForEqualLabels()
    {
        var process = Seed();

        var ids = new List<Guid> { process.Id.Value };
        ids.AddRange(process.BacklogLevels.Select(level => level.Id));
        ids.AddRange(process.WorkItemTypes.Select(type => type.Id));
        ids.AddRange(process.WorkItemTypes.SelectMany(type => type.States).Select(state => state.Id));

        Assert.Equal(1 + 4 + 5 + 24, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, ids);

        var newStates = process.WorkItemTypes.SelectMany(type => type.States).Where(state => state.Name == "Новая").ToList();
        var resolvedStates = process.WorkItemTypes.SelectMany(type => type.States).Where(state => state.Name == "Разрешена").ToList();

        Assert.Equal(5, newStates.Count);
        Assert.Equal(5, newStates.Select(state => state.Id).Distinct().Count());
        Assert.Equal(4, resolvedStates.Count);
        Assert.Equal(4, resolvedStates.Select(state => state.Id).Distinct().Count());
    }

    [Fact]
    public void Seed_NewOrganization_EveryChildCarriesTheProcessAndItsParent()
    {
        var id = ProcessId.New();

        var process = ProcessDefinition.Seed(id, OrganizationId);

        Assert.Equal(id, process.Id);
        Assert.All(process.BacklogLevels, level => Assert.Equal(id, level.ProcessId));
        Assert.All(process.WorkItemTypes, type => Assert.Equal(id, type.ProcessId));
        Assert.All(process.WorkItemTypes, type => Assert.Contains(process.BacklogLevels, level => level.Id == type.BacklogLevelId));
        Assert.All(process.WorkItemTypes, type => Assert.All(type.States, state => Assert.Equal(type.Id, state.WorkItemTypeId)));
    }

    // US-1 · "Each organization identifier gets a process of its own": same content, no shared identity (BR-M2-1).
    [Fact]
    public void Seed_TwoOrganizations_ShareNoIdentity()
    {
        var first = Seed(Guid.Parse("a1b2c3d4-0000-4000-8000-00000000000a"));
        var second = Seed(Guid.Parse("a1b2c3d4-0000-4000-8000-00000000000b"));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Empty(IdsOf(first).Intersect(IdsOf(second)));

        Assert.Equal(
            first.WorkItemTypes.Select(type => $"{type.Key} {StatesOf(type)}"),
            second.WorkItemTypes.Select(type => $"{type.Key} {StatesOf(type)}"));
    }

    // US-1 · "A malformed identifier is refused and nothing is created": the empty identifier (status.md D-10).
    [Fact]
    public void Seed_EmptyOrganizationId_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => ProcessDefinition.Seed(ProcessId.New(), Guid.Empty));

        Assert.Equal("organizationId", exception.ParamName);
    }

    [Fact]
    public void Seed_EmptyProcessId_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProcessDefinition.Seed(default, OrganizationId));
    }

    // US-1 · the blueprint checks itself at every seed (plan-backend.md § 3), so the shipped blueprint must be valid.
    [Fact]
    public void Blueprint_AsShipped_SatisfiesItsInvariants()
    {
        var exception = Record.Exception(SeededProcess.Blueprint.EnsureValid);

        Assert.Null(exception);
    }

    private static IEnumerable<Guid> IdsOf(ProcessDefinition process) =>
        process.BacklogLevels.Select(level => level.Id)
            .Concat(process.WorkItemTypes.Select(type => type.Id))
            .Concat(process.WorkItemTypes.SelectMany(type => type.States).Select(state => state.Id));
}
