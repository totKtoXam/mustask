using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Boards;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Microsoft.Extensions.Logging;

namespace Mustask.Application.Tests.TestDoubles;

/// <summary>
/// The fakes of mod-005's handler tests (Delete, Restore, Purge): one organization with one project «MSK» and the process of one
/// type «US» whose states are «Новая», «Активна» and «Удалена», a work item repository that records the calls it receives in the
/// order they arrive, and the seams around it. The handlers run against them with no database: what the tests pin is the order
/// and the arguments of the calls, which is the handlers' own business (the locks' effect is the integration tests').
/// </summary>
public sealed class WorkItemWorld
{
    public const string ProjectKey = "MSK";

    public Guid OrganizationId { get; } = Guid.CreateVersion7();

    public Guid ProjectId { get; } = Guid.CreateVersion7();

    public Guid RootAreaId { get; } = Guid.CreateVersion7();

    public Guid LevelId { get; } = Guid.CreateVersion7();

    public Guid StoryTypeId { get; } = Guid.CreateVersion7();

    public Guid NewId { get; } = Guid.CreateVersion7();

    public Guid ActiveId { get; } = Guid.CreateVersion7();

    public Guid RemovedId { get; } = Guid.CreateVersion7();

    public List<string> Calls { get; } = [];

    public ListLogger<T> Logger<T>() => new(Calls);

    public ProcessDefinitionRow Process => new(
        new ProcessId(Guid.CreateVersion7()),
        [new BacklogLevelRow(LevelId, "Истории", 2, BacklogLevelKind.Requirement)],
        [
            new WorkItemTypeRow(
                StoryTypeId,
                "US",
                "Пользовательская история",
                AppearanceToken.Story,
                LevelId,
                "Истории",
                [
                    new StateRow(NewId, "Новая", 1, StateCategory.Proposed),
                    new StateRow(ActiveId, "Активна", 2, StateCategory.InProgress),
                    new StateRow(RemovedId, "Удалена", 3, StateCategory.Removed),
                ]),
        ]);

    public WorkItemTypeStates StoryType => ProcessWorkItemTypes.OfItem(Process, StoryTypeId);

    public WorldStructure Structure => new(this);

    public FakeProcessReadModel Processes => new(Process);

    public WorkItem LiveStory(int number = 12) =>
        WorkItem.Create(ProjectId, number, StoryType, RootAreaId, "История", Guid.CreateVersion7(), new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), "creator-sub");

    public WorkItem DeletedStory(int number = 12)
    {
        var item = LiveStory(number);
        item.Delete(StoryType, "deleter-sub", new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero));

        return item;
    }
}

public sealed class WorldStructure(WorkItemWorld world) : IOrganizationStructure
{
    public Task<OrganizationInfo?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    // feat-028 D-11: the read takes the organization and answers null for another organization's project.
    public Task<ProjectInfo?> FindProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<ProjectInfo?>(
            organizationId == world.OrganizationId && projectId == world.ProjectId
                ? new ProjectInfo(
                    world.ProjectId, world.OrganizationId, "Мастаск", WorkItemWorld.ProjectKey, ProjectStatus.Active, true, null, world.RootAreaId, "creator-sub", DateTimeOffset.UnixEpoch)
                : null);

    public Task<TeamInfo?> FindTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ProjectTeamInfo?> FindProjectTeamAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<TeamInfo>> ListLinkedTeamsAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<LinkedProjectInfo>> ListLinkedProjectsAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

public sealed class FakeProcessReadModel(ProcessDefinitionRow process) : IProcessDefinitionReadModel
{
    public Task<ProcessDefinitionRow?> FindByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<ProcessDefinitionRow?>(process);
}

/// <summary>
/// Records the calls the purge and the one-row writes make, in order, into the world's shared log. <see cref="Locked"/> is what
/// <c>LockAsync</c> and <c>FindLockedAsync</c> answer; <see cref="ChildIds"/> what the purge's locking statement answers
/// (<c>null</c> for no row).
/// </summary>
public sealed class FakeWorkItemRepository(WorkItemWorld world) : IWorkItemRepository
{
    public WorkItem? Locked { get; set; }

    public IReadOnlyList<Guid>? ChildIds { get; set; } = [];

    public int SaveCount { get; private set; }

    public bool Removed { get; private set; }

    public Task<bool> LockTeamLinkAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Guid?> LockProjectLinksAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task LockNumberSequenceAsync(Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<int> TakeNextNumberAsync(Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Guid?> FindCreatedByRequestAsync(Guid projectId, Guid requestId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<WorkItem?> LockAsync(Guid projectId, int number, CancellationToken cancellationToken)
    {
        world.Calls.Add($"lock-item {number}");

        return Task.FromResult(Locked);
    }

    public Task<bool> LockAreaAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task LockHierarchyAsync(Guid projectId, CancellationToken cancellationToken)
    {
        world.Calls.Add("lock-hierarchy");

        return Task.CompletedTask;
    }

    public Task<ParentWrite?> LockParentWriteAsync(WorkItem child, Guid? newParentId, Guid? seenParentId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<WorkItem?> LockNewParentAsync(Guid projectId, Guid parentId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyCollection<HierarchyLink>> AncestryAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Guid>?> LockForPurgeAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken)
    {
        world.Calls.Add("lock-item-and-children");

        return Task.FromResult(ChildIds);
    }

    public Task<WorkItem?> FindLockedAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken)
    {
        world.Calls.Add("find-locked");

        return Task.FromResult(Locked);
    }

    public Task ReleaseChildrenAsync(Guid projectId, Guid workItemId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        world.Calls.Add($"release-children {now:O}");

        return Task.CompletedTask;
    }

    public void Add(WorkItem item) => throw new NotSupportedException();

    public void Remove(WorkItem item)
    {
        Removed = true;
        world.Calls.Add("remove");
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        world.Calls.Add("save");

        return Task.CompletedTask;
    }
}

/// <summary>Answers the head a test sets and the view of the item a write has just made; every other read is not used here.</summary>
public sealed class FakeWorkItemReadModel(WorkItemWorld world) : IWorkItemReadModel
{
    public WorkItemHead? Head { get; set; }

    public Task<WorkItemView?> FindAsync(Guid projectId, int number, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<WorkItemHead?> FindHeadAsync(Guid projectId, int number, CancellationToken cancellationToken)
    {
        world.Calls.Add($"read-head {number}");

        return Task.FromResult(Head);
    }

    public Task<WorkItemRefView> GetRefAsync(Guid workItemId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ParentCandidates> FindParentCandidatesAsync(ParentCandidateSearch search, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<WorkItemView> GetAsync(Guid workItemId, CancellationToken cancellationToken) =>
        Task.FromResult(
            new WorkItemView(
                workItemId,
                "MSK-US-12",
                12,
                world.ProjectId,
                new WorkItemTypeView(world.StoryTypeId, "US", "Пользовательская история", "story"),
                "История",
                string.Empty,
                new WorkItemStateView(world.NewId, "Новая", new WorkItemStateCategoryView("Proposed", "Предложенные")),
                null,
                new WorkItemAreaView(world.RootAreaId, "Мастаск"),
                null,
                "creator-sub",
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                null,
                [])
            {
                Priority = 2,
                Risk = null,
                StoryPoints = null,
                Effort = null,
                BusinessValue = null,
                TimeCriticality = null,
                StartDate = null,
                TargetDate = null,
                Severity = null,
                Activity = null,
                OriginalEstimate = null,
                RemainingWork = null,
                CompletedWork = null,
                ValueArea = null,
                AcceptanceCriteria = null,
                ReproSteps = null,
                SystemInfo = null,
                Deletion = null,
            });

    public Task<WorkItemPage> GetPageAsync(Guid projectId, int page, int pageSize, WorkItemFilter filter, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<AssigneeFilterOptionView>> GetAssigneeOptionsAsync(
        Guid projectId, IReadOnlyCollection<string> stateNames, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

public sealed class WorldTeamPlaces(WorkItemWorld world) : ITeamBacklogPlaces
{
    public Task DropForItemAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        world.Calls.Add("drop-team-place");

        return Task.CompletedTask;
    }

    public Task DropForTeamAsync(Guid teamId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task DropForTeamInProjectAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
}

/// <summary>A purge step that writes its calls into the world's log and can be made to refuse or to fail.</summary>
public sealed class RecordingPurgeStep(WorkItemWorld world, string name) : IWorkItemPurgeStep
{
    public Exception? PreflightFailure { get; set; }

    public Exception? PurgeFailure { get; set; }

    public WorkItemPurgeRequest? SeenRequest { get; private set; }

    public WorkItemPurge? SeenPurge { get; private set; }

    public Task PreflightAsync(WorkItemPurgeRequest request, CancellationToken cancellationToken)
    {
        SeenRequest = request;
        world.Calls.Add($"preflight {name}");

        return PreflightFailure is null ? Task.CompletedTask : Task.FromException(PreflightFailure);
    }

    public Task PurgeAsync(WorkItemPurge purge, CancellationToken cancellationToken)
    {
        SeenPurge = purge;
        world.Calls.Add($"step {name}");

        return PurgeFailure is null ? Task.CompletedTask : Task.FromException(PurgeFailure);
    }
}

/// <summary>An <see cref="ILogger{T}"/> that writes each formatted message into the world's shared log, as "log: …".</summary>
public sealed class ListLogger<T>(List<string> calls) : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);

        Messages.Add(message);
        calls.Add($"log: {message}");
    }
}
