using Mustask.Application.Tests.TestDoubles.Iterations;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.RichText;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Boards;
using Mustask.Application.Features.Organizations;
using Mustask.Application.Features.WorkItems.SaveWorkItem;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.WorkItems.SaveWorkItem;

/// <summary>
/// feat-018 US-4, US-3 (server half): how the save prepares the description before the transaction and in what order
/// it refuses it. The reduction itself is a fake here; its rules are the Infrastructure tests'.
/// </summary>
public sealed class SaveWorkItemDescriptionHandlerTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    private readonly Guid organizationId = Guid.CreateVersion7();
    private readonly Guid projectId = Guid.CreateVersion7();
    private readonly WorkItemTypeStates type;
    private readonly WorkItem item;
    private readonly Fixture fixture;

    public SaveWorkItemDescriptionHandlerTests()
    {
        type = new WorkItemTypeStates(
            Guid.CreateVersion7(),
            "TASK",
            AppearanceToken.Task,
            [
                new WorkItemTypeState(Guid.CreateVersion7(), 1, StateCategory.Proposed),
                new WorkItemTypeState(Guid.CreateVersion7(), 2, StateCategory.Completed),
            ]);

        item = WorkItem.Create(projectId, 1, type, Guid.CreateVersion7(), "Заголовок", Guid.CreateVersion7(), Created, "sub");
        fixture = new Fixture(this);
    }

    private SaveWorkItemCommand Command(string? description = null, string? format = null, string? title = null, string? identifier = "MUS-TASK-1") =>
        new(organizationId.ToString(), projectId.ToString(), identifier, Title: title, Description: description, DescriptionFormat: format);

    // US-4 · "A client that declares nothing is unchanged": plain, as sent, no reducer.
    [Fact]
    public async Task Handle_NoFormat_StoresTheValueAsSent_Plain_WithoutReducing()
    {
        var result = await fixture.Handler.Handle(Command("a < b <b>x</b>"), CancellationToken.None);

        Assert.Equal(("a < b <b>x</b>", "plain"), (result.Item.Description, result.Item.DescriptionFormat));
        Assert.Empty(fixture.Reducer.Calls);
    }

    // US-4 · "a plain description over 100 000 code units is refused with description_too_long as before".
    [Fact]
    public async Task Handle_PlainOver100000_IsDescriptionTooLong_NothingStored()
    {
        var failure = await Assert.ThrowsAsync<ValidationException>(
            () => fixture.Handler.Handle(Command(new string('a', 100_001)), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.DescriptionTooLong, failure.Errors["description"][0].Code);
        Assert.Equal(string.Empty, item.Description);
        Assert.Equal(0, fixture.Repository.Saves);
    }

    // US-4 · "A declared html description is reduced and reported", and the reduction runs outside the transaction.
    [Fact]
    public async Task Handle_Html_StoresTheReducedValue_ReducedOutsideTheTransaction()
    {
        fixture.Reducer.Result = new RichTextReduction(false, "<p>a</p>", 8, 1, true);

        var result = await fixture.Handler.Handle(Command("<p>a</p><script>x</script>", "html"), CancellationToken.None);

        Assert.Equal(("<p>a</p>", "html"), (result.Item.Description, result.Item.DescriptionFormat));
        Assert.Equal(["<p>a</p><script>x</script>"], fixture.Reducer.Calls);
        Assert.Equal([false], fixture.Reducer.InTransactionAtCall);
        Assert.Equal(WorkItemLimits.DescriptionMarkupMaxLength, fixture.Reducer.LastLimits!.MaxMarkupCodePoints);
    }

    // US-4 · "An unknown format is refused" and "An unknown format with no description is still refused": nothing
    // stored, the reducer never asked.
    [Theory]
    [InlineData("markdown", "x")]
    [InlineData("HTML", "x")]
    [InlineData("", "x")]
    [InlineData(" html", "x")]
    [InlineData("markdown", null)]
    public async Task Handle_UnknownFormat_IsFormatUnknown_WithOrWithoutDescription(string format, string? description)
    {
        var failure = await Assert.ThrowsAsync<ValidationException>(
            () => fixture.Handler.Handle(Command(description, format), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.DescriptionFormatUnknown, failure.Errors["description"][0].Code);
        Assert.Empty(fixture.Reducer.Calls);
        Assert.Equal(0, fixture.Repository.Saves);
    }

    // The unknown format wins over a limit: the description is not interpreted.
    [Fact]
    public async Task Handle_UnknownFormat_WinsOverAnOversizedDescription()
    {
        var failure = await Assert.ThrowsAsync<ValidationException>(
            () => fixture.Handler.Handle(Command(new string('a', 2_000_001), "markdown"), CancellationToken.None));

        Assert.Equal([WorkItemErrorCodes.DescriptionFormatUnknown], failure.Errors["description"].Select(error => error.Code));
    }

    // US-4 · "A format with no description changes nothing".
    [Theory]
    [InlineData("html")]
    [InlineData("plain")]
    public async Task Handle_ValidFormat_WithNoDescription_ChangesNothing(string format)
    {
        item.Save(new WorkItemEdit(Description: "<p>kept</p>", DescriptionFormat: DescriptionFormat.Html), type, Created);

        var result = await fixture.Handler.Handle(Command(null, format, title: "Новый"), CancellationToken.None);

        Assert.Equal(("<p>kept</p>", "html", "Новый"), (result.Item.Description, result.Item.DescriptionFormat, result.Item.Title));
        Assert.Empty(fixture.Reducer.Calls);
    }

    // US-4 · "An oversized request is refused before parsing": the reducer is not called.
    [Fact]
    public async Task Handle_HtmlOver2000000Units_IsMarkupTooLarge_NotParsed()
    {
        var failure = await Assert.ThrowsAsync<ValidationException>(
            () => fixture.Handler.Handle(Command(new string('a', 2_000_001), "html"), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.DescriptionMarkupTooLarge, failure.Errors["description"][0].Code);
        Assert.Empty(fixture.Reducer.Calls);
    }

    // US-2 · "The reduction stops early" / US-4 · "Amplifying input is refused promptly".
    [Fact]
    public async Task Handle_ReductionExceeded_IsMarkupTooLarge()
    {
        fixture.Reducer.Result = RichTextReduction.OverTheBound;

        var failure = await Assert.ThrowsAsync<ValidationException>(
            () => fixture.Handler.Handle(Command("<b>x</b>", "html"), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.DescriptionMarkupTooLarge, failure.Errors["description"][0].Code);
    }

    // US-4 · "The visible limit is enforced" and "Over both limits, the markup reason wins".
    [Theory]
    [InlineData(500_000, 100_001, WorkItemErrorCodes.DescriptionTooLong)]
    [InlineData(1_000_001, 100_001, WorkItemErrorCodes.DescriptionMarkupTooLarge)]
    [InlineData(1_000_001, 50_000, WorkItemErrorCodes.DescriptionMarkupTooLarge)]
    public async Task Handle_ReducedOverALimit_OneReason_MarkupFirst(int markup, int visible, string expected)
    {
        fixture.Reducer.Result = new RichTextReduction(false, "<p>x</p>", markup, visible, true);

        var failure = await Assert.ThrowsAsync<ValidationException>(
            () => fixture.Handler.Handle(Command("<p>x</p>", "html"), CancellationToken.None));

        Assert.Equal([expected], failure.Errors["description"].Select(error => error.Code));
    }

    // US-4 · "A never-written or cleared description is plain and empty" (BR-RT-6): no content is "" plain.
    [Fact]
    public async Task Handle_HtmlWithNoContent_IsStoredAsEmptyPlain()
    {
        item.Save(new WorkItemEdit(Description: "<p>was</p>", DescriptionFormat: DescriptionFormat.Html), type, Created);
        fixture.Reducer.Result = new RichTextReduction(false, "<p></p>", 7, 0, false);

        var result = await fixture.Handler.Handle(Command("<p> </p>", "html"), CancellationToken.None);

        Assert.Equal((string.Empty, "plain"), (result.Item.Description, result.Item.DescriptionFormat));
    }

    // mod-003 § 0.4: the description's reason joins the title's in one 400.
    [Fact]
    public async Task Handle_TitleAndDescriptionBothInvalid_ReportsBothReasonsAtOnce()
    {
        var failure = await Assert.ThrowsAsync<ValidationException>(
            () => fixture.Handler.Handle(Command("x", "markdown", title: " "), CancellationToken.None));

        Assert.Equal(WorkItemErrorCodes.TitleRequired, failure.Errors["title"][0].Code);
        Assert.Equal(WorkItemErrorCodes.DescriptionFormatUnknown, failure.Errors["description"][0].Code);
    }

    // mod-003 § 0.4: a bad path with an oversized description still answers 404, not 400.
    [Fact]
    public async Task Handle_BadPath_WithOversizedDescription_Is404()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => fixture.Handler.Handle(Command(new string('a', 2_000_001), "html", identifier: "not-an-identifier"), CancellationToken.None));
    }

    // --- doubles -------------------------------------------------------------------------------------------------

    private sealed class Fixture
    {
        public Fixture(SaveWorkItemDescriptionHandlerTests test)
        {
            var project = new ProjectInfo(
                test.projectId, test.organizationId, "Проект", "MUS", ProjectStatus.Active, IsReachable: true,
                Guid.CreateVersion7(), Guid.CreateVersion7(), "sub", Created);

            UnitOfWork = new RecordingUnitOfWork();
            Reducer = new FakeReducer(UnitOfWork);
            Repository = new FakeRepository(test.item);

            Handler = new SaveWorkItemHandler(
                new FakeStructure(project),
                new FakeProcesses(test.type),
                new FakeDirectory(),
                new FakeMemberReadModel(new InMemoryOrganizations(), new FakeDirectory()),
                Repository,
                new FakeReadModel(test.item, test.type, project),
                new FakeTeamPlaces(),
                new FakeSprintRepository(),
                new FakeOrganizationAccess(OrganizationRole.Admin),
                new FakeClock(),
                UnitOfWork,
                Reducer);
        }

        public SaveWorkItemHandler Handler { get; }

        public FakeReducer Reducer { get; }

        public FakeRepository Repository { get; }

        public RecordingUnitOfWork UnitOfWork { get; }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public bool InTransaction { get; private set; }

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken)
        {
            InTransaction = true;

            try
            {
                return await action(cancellationToken);
            }
            finally
            {
                InTransaction = false;
            }
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeReducer(RecordingUnitOfWork unitOfWork) : IRichTextReducer
    {
        public List<string> Calls { get; } = [];

        public List<bool> InTransactionAtCall { get; } = [];

        public RichTextReductionLimits? LastLimits { get; private set; }

        public RichTextReduction Result { get; set; } = new(false, "<p>x</p>", 8, 1, true);

        public Task<RichTextReduction> ReduceAsync(string html, RichTextReductionLimits limits, CancellationToken cancellationToken)
        {
            Calls.Add(html);
            InTransactionAtCall.Add(unitOfWork.InTransaction);
            LastLimits = limits;

            return Task.FromResult(Result);
        }
    }

    private sealed class FakeStructure(ProjectInfo project) : IOrganizationStructure
    {
        public Task<OrganizationInfo?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectInfo?> FindProjectAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectInfo?>(organizationId == project.OrganizationId && projectId == project.Id ? project : null);

        public Task<TeamInfo?> FindTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectTeamInfo?> FindProjectTeamAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TeamInfo>> ListLinkedTeamsAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<LinkedProjectInfo>> ListLinkedProjectsAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AreaInfo?> FindAreaAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<AreaInfo>> ListAreasOfTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTeamPlaces : ITeamBacklogPlaces
    {
        public Task DropForItemAsync(Guid workItemId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DropForTeamAsync(Guid teamId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DropForTeamInProjectAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeProcesses(WorkItemTypeStates type) : IProcessDefinitionReadModel
    {
        public Task<ProcessDefinitionRow?> FindByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken)
        {
            var levelId = Guid.CreateVersion7();
            var states = type.States.Select(state => new StateRow(state.Id, "S" + state.Position, state.Position, state.Category)).ToList();

            return Task.FromResult<ProcessDefinitionRow?>(new ProcessDefinitionRow(
                new ProcessId(Guid.CreateVersion7()),
                [new BacklogLevelRow(levelId, "Задачи", 1, BacklogLevelKind.Task)],
                [new WorkItemTypeRow(type.TypeId, type.TypeKey, "Задача", type.Appearance, levelId, "Задачи", states)]));
        }
    }

    private sealed class FakeDirectory : IUserDirectory
    {
        public Task<UserSearchResult> SearchAsync(UserSearch search, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<UserLookupResult> LookupAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeRepository(WorkItem item) : IWorkItemRepository
    {
        public int Saves { get; private set; }

        public Task<WorkItem?> LockAsync(Guid projectId, int number, CancellationToken cancellationToken) =>
            Task.FromResult<WorkItem?>(projectId == item.ProjectId && number == item.Number ? item : null);

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            Saves++;

            return Task.CompletedTask;
        }

        public Task<bool> LockTeamLinkAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Guid?> LockProjectLinksAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>?> LockForPurgeAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkItem?> FindLockedAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReleaseChildrenAsync(Guid projectId, Guid workItemId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Remove(WorkItem item) => throw new NotSupportedException();

        public Task LockNumberSequenceAsync(Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> TakeNextNumberAsync(Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Guid?> FindCreatedByRequestAsync(Guid projectId, Guid requestId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> LockAreaAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task LockHierarchyAsync(Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ParentWrite?> LockParentWriteAsync(WorkItem child, Guid? newParentId, Guid? seenParentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkItem?> LockNewParentAsync(Guid projectId, Guid parentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<HierarchyLink>> AncestryAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(WorkItem item) => throw new NotSupportedException();
    }

    private sealed class FakeReadModel(WorkItem item, WorkItemTypeStates type, ProjectInfo project) : IWorkItemReadModel
    {
        public Task<WorkItemView> GetAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            var typeView = new WorkItemTypeView(type.TypeId, type.TypeKey, "Задача", "task");
            var state = new WorkItemStateView(item.StateId, "Новая", new WorkItemStateCategoryView("Proposed", "Предложено"));

            return Task.FromResult(new WorkItemView(
                item.Id,
                WorkItemIdentifier.Format(project.Key, type.TypeKey, item.Number),
                item.Number,
                item.ProjectId,
                typeView,
                item.Title,
                item.Description,
                state,
                null,
                new WorkItemAreaView(item.AreaId, "Проект"),
                null,
                item.CreatedBy,
                item.CreatedAt,
                item.ChangedAt,
                null,
                [])
            {
                DescriptionFormat = DescriptionFormats.Key(item.DescriptionFormat),
                Priority = item.Priority,
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
                Project = new WorkItemProjectView(project.Id, "Проект"),
            });
        }

        public Task<WorkItemView?> FindAsync(Guid projectId, int number, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WorkItemHead?> FindHeadAsync(Guid projectId, int number, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WorkItemRefView> GetRefAsync(Guid workItemId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ParentCandidates> FindParentCandidatesAsync(ParentCandidateSearch search, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<WorkItemPage> GetPageAsync(Guid projectId, int page, int pageSize, WorkItemFilter filter, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AssigneeFilterOptionView>> GetAssigneeOptionsAsync(
            Guid projectId, IReadOnlyCollection<string> stateNames, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
