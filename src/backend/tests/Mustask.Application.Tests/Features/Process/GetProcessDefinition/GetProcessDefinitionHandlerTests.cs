using Mustask.Application.Features.Process.GetProcessDefinition;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Process.GetProcessDefinition;

/// <summary>US-2 (mod-001-process-seed): the read handler, with a fake read model.</summary>
public sealed class GetProcessDefinitionHandlerTests
{
    private const string KnownOrganization = "d0000000-0000-4000-8000-000000000001";

    private static GetProcessDefinitionHandler NewHandler(FakeProcessDefinitionReadModel readModel) => new(readModel);

    private static WorkItemTypeResponse Type(ProcessDefinitionResponse response, string key) =>
        response.WorkItemTypes.Single(type => type.Key == key);

    private static string Category(WorkItemTypeResponse type, string stateName) =>
        type.States.Single(state => state.Name == stateName).Category.Label;

    // US-2 · "A seeded process is shown in full" (backend half): five types in the order of the rows, the key,
    // the appearance token, the level, and the four levels with their kinds, all as strings.
    [Fact]
    public async Task Handle_FoundProcess_MapsLabelsTokensKindsAndInitialMarker()
    {
        var readModel = FakeProcessDefinitionReadModel.Seeded();
        var handler = NewHandler(readModel);

        var response = await handler.Handle(new GetProcessDefinitionQuery(KnownOrganization), CancellationToken.None);

        Assert.Equal(readModel.Answer!.Id.Value, response.Id);
        Assert.Equal(
            ["EPIC", "FEAT", "US", "BUG", "TASK"],
            response.WorkItemTypes.Select(type => type.Key));
        Assert.Equal(
            ["epic", "feature", "story", "bug", "task"],
            response.WorkItemTypes.Select(type => type.Appearance));
        Assert.Equal(
            [("Эпики", 4, "portfolio"), ("Фичи", 3, "portfolio"), ("Истории", 2, "requirement"), ("Задачи", 1, "task")],
            response.BacklogLevels.Select(level => (level.Name, level.Rank, level.Kind)));
        Assert.Equal(
            ["Эпики", "Фичи", "Истории", "Истории", "Задачи"],
            response.WorkItemTypes.Select(type => type.BacklogLevel.Name));
        Assert.All(response.WorkItemTypes, type =>
            Assert.Contains(response.BacklogLevels, level => level.Id == type.BacklogLevel.Id && level.Name == type.BacklogLevel.Name));

        // BR-M2-2: exactly one initial state per type, the first, and the order is 1-based and dense.
        Assert.All(response.WorkItemTypes, type =>
        {
            Assert.Equal(Enumerable.Range(1, type.States.Count), type.States.Select(state => state.Order));
            Assert.Equal([true], type.States.Where(state => state.IsInitial).Select(state => state.IsInitial));
            Assert.True(type.States[0].IsInitial);
            Assert.Equal("Удалена", type.States[^1].Name);
        });
    }

    // US-2 · "Categories are shown by their Russian labels": the label comes from the domain, the code beside it.
    [Fact]
    public async Task Handle_FoundProcess_TaskStatesCarryRussianCategoryLabelsAndCodes()
    {
        var handler = NewHandler(FakeProcessDefinitionReadModel.Seeded());

        var response = await handler.Handle(new GetProcessDefinitionQuery(KnownOrganization), CancellationToken.None);

        var task = Type(response, "TASK");
        Assert.Equal(
            [("Новая", "Proposed", "Новые"), ("Активна", "InProgress", "В работе"), ("Закрыта", "Completed", "Завершённые"), ("Удалена", "Removed", "Удалённые")],
            task.States.Select(state => (state.Name, state.Category.Code, state.Category.Label)));
    }

    // US-2 · "One state label belongs to two categories" (doc-010 D-4).
    [Fact]
    public async Task Handle_FoundProcess_ResolvedOfStoryAndBugDifferInCategory()
    {
        var handler = NewHandler(FakeProcessDefinitionReadModel.Seeded());

        var response = await handler.Handle(new GetProcessDefinitionQuery(KnownOrganization), CancellationToken.None);

        Assert.Equal("В работе", Category(Type(response, "US"), "Разрешена"));
        Assert.Equal("Решённые", Category(Type(response, "BUG"), "Разрешена"));
    }

    // Design R-25: the server fixes the order, the handler keeps what the read model gave and sorts nothing.
    [Fact]
    public async Task Handle_FoundProcess_KeepsTheOrderOfTheRows()
    {
        var readModel = FakeProcessDefinitionReadModel.Seeded();
        var row = readModel.Answer!;
        readModel.Answer = row with { WorkItemTypes = [.. row.WorkItemTypes.Reverse()], BacklogLevels = [.. row.BacklogLevels.Reverse()] };
        var handler = NewHandler(readModel);

        var response = await handler.Handle(new GetProcessDefinitionQuery(KnownOrganization), CancellationToken.None);

        Assert.Equal(["TASK", "BUG", "US", "FEAT", "EPIC"], response.WorkItemTypes.Select(type => type.Key));
        Assert.Equal([1, 2, 3, 4], response.BacklogLevels.Select(level => level.Rank));
    }

    // US-2 · "An identifier with no process is not found": a malformed value never reaches the storage.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("d00000000000400080000000000000001")]
    [InlineData("{d0000000-0000-4000-8000-000000000001}")]
    [InlineData("d0000000-0000-4000-8000-00000000001")]
    [InlineData("d0000000-0000-4000-8000-000000000001x")]
    public async Task Handle_MalformedId_ThrowsNotFoundWithoutReading(string? organizationId)
    {
        var readModel = FakeProcessDefinitionReadModel.Seeded();
        var handler = NewHandler(readModel);

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetProcessDefinitionQuery(organizationId), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, error.Code);
        Assert.Empty(readModel.Asked);
    }

    // US-2 · "An identifier with no process is not found": the same code for an unknown identifier.
    [Fact]
    public async Task Handle_UnknownId_ThrowsTheSameNotFound()
    {
        var readModel = new FakeProcessDefinitionReadModel { Answer = null };
        var handler = NewHandler(readModel);

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new GetProcessDefinitionQuery(KnownOrganization), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, error.Code);
        Assert.Equal([Guid.Parse(KnownOrganization)], readModel.Asked);
    }

    [Fact]
    public async Task Handle_UppercaseCanonicalId_IsAskedForAsTheSameGuid()
    {
        var readModel = FakeProcessDefinitionReadModel.Seeded();
        var handler = NewHandler(readModel);

        await handler.Handle(new GetProcessDefinitionQuery(KnownOrganization.ToUpperInvariant()), CancellationToken.None);

        Assert.Equal([Guid.Parse(KnownOrganization)], readModel.Asked);
    }

    // US-2 · "A failed read is not taken for a missing organization": the failure stays a failure.
    [Fact]
    public async Task Handle_ReadFails_PropagatesTheFailureAndDoesNotBecomeNotFound()
    {
        var failure = new InvalidOperationException("storage unreachable");
        var handler = NewHandler(new FakeProcessDefinitionReadModel { ThrowOnRead = failure });

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new GetProcessDefinitionQuery(KnownOrganization), CancellationToken.None));

        Assert.Same(failure, thrown);
    }
}
