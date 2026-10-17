using Mustask.Domain.Boards;

namespace Mustask.Domain.Tests.Boards;

/// <summary>
/// The backlog page's tree at full depth (feat-027 BR-T-4..-6), without the
/// database: the tree set is the team's live items of the page's level and every
/// level below; an item stands beneath a parent in the set of its own level or a
/// higher one; the top-level rows are the page level's items that stand beneath
/// none. feat-027 US-1's data, «Платформа»'s items only (MSK-FEAT-21 and
/// MSK-TASK-7 are «Мастаск»'s, so no entries): «Эпики» MSK-EPIC-1, MSK-EPIC-20;
/// «Фичи» MSK-FEAT-8 (placed first), MSK-FEAT-6 (placed second), MSK-FEAT-23
/// (unplaced); «Истории» MSK-US-2, MSK-BUG-4, MSK-BUG-5, MSK-US-22; «Задачи»
/// MSK-TASK-3. Ranks as seeded: epic 4, feature 3, story and bug 2, task 1.
/// </summary>
public sealed class BacklogTreeTests
{
    private const int EpicRank = 4;
    private const int FeatureRank = 3;
    private const int StoryRank = 2;
    private const int TaskRank = 1;

    private static readonly Guid Epic1 = Guid.CreateVersion7();
    private static readonly Guid Epic20 = Guid.CreateVersion7();
    private static readonly Guid Feature6 = Guid.CreateVersion7();
    private static readonly Guid Feature8 = Guid.CreateVersion7();
    private static readonly Guid Feature21 = Guid.CreateVersion7();
    private static readonly Guid Feature23 = Guid.CreateVersion7();
    private static readonly Guid Story2 = Guid.CreateVersion7();
    private static readonly Guid Bug4 = Guid.CreateVersion7();
    private static readonly Guid Bug5 = Guid.CreateVersion7();
    private static readonly Guid Story22 = Guid.CreateVersion7();
    private static readonly Guid Task3 = Guid.CreateVersion7();
    private static readonly Guid Task7 = Guid.CreateVersion7();

    /// <summary>
    /// US-1's links: MSK-EPIC-1 holds MSK-FEAT-8, MSK-FEAT-6, MSK-FEAT-21 and MSK-US-22; MSK-FEAT-8 MSK-FEAT-23;
    /// MSK-FEAT-6 MSK-US-2 and MSK-BUG-4; MSK-US-2 MSK-BUG-5, MSK-TASK-3 and MSK-TASK-7.
    /// </summary>
    private static readonly BacklogTreeEntry[] Epics = [new(Epic1, 1, null, null, EpicRank), new(Epic20, 20, null, null, EpicRank)];

    private static readonly BacklogTreeEntry[] Features =
        [new(Feature6, 6, 2, Epic1, FeatureRank), new(Feature8, 8, 1, Epic1, FeatureRank), new(Feature23, 23, null, Feature8, FeatureRank)];

    private static readonly BacklogTreeEntry[] Stories =
    [
        new(Bug4, 4, null, Feature6, StoryRank), new(Story2, 2, null, Feature6, StoryRank),
        new(Bug5, 5, null, Story2, StoryRank), new(Story22, 22, null, Epic1, StoryRank),
    ];

    private static readonly BacklogTreeEntry[] Tasks = [new(Task3, 3, null, Story2, TaskRank)];

    private static string Shape(IReadOnlyList<BacklogTreeNode> rows) =>
        string.Join(' ', rows.Select(row => row.Children.Count == 0 ? Name(row.ItemId) : $"{Name(row.ItemId)}[{Shape(row.Children)}]"));

    private static string Name(Guid id) =>
        new Dictionary<Guid, string>
        {
            [Epic1] = "E1", [Epic20] = "E20", [Feature6] = "F6", [Feature8] = "F8", [Feature21] = "F21", [Feature23] = "F23",
            [Story2] = "US2", [Bug4] = "BUG4", [Bug5] = "BUG5", [Story22] = "US22", [Task3] = "T3", [Task7] = "T7",
        }.TryGetValue(id, out var name) ? name : id.ToString();

    private static IReadOnlyList<BacklogTreeNode> Nest(int levelRank, params BacklogTreeEntry[][] levels) =>
        BacklogTree.Nest(levelRank, levels.SelectMany(level => level).ToList());

    // US-2 · "The «Эпики» tree reaches the tasks" and "Children stand by level, then in their backlog's order": the
    // features in the «Фичи» order, then MSK-US-22 (a story directly under the epic); beneath MSK-US-2 its bug before
    // its task; MSK-FEAT-23 beneath MSK-FEAT-8 (its own level).
    [Fact]
    public void Nest_Epics_FullDepth_ByLevelThenEachBacklogsOrder()
    {
        Assert.Equal(
            "E1[F8[F23] F6[US2[BUG5 T3] BUG4] US22] E20",
            Shape(Nest(EpicRank, Epics, Features, Stories, Tasks)));
    }

    // US-2 · "A same-level child stands beneath its parent only": on «Фичи» MSK-FEAT-23 is beneath MSK-FEAT-8 and not a
    // top-level row; MSK-FEAT-6 shows its stories, MSK-US-2 its bug and task.
    [Fact]
    public void Nest_Features_ASameLevelChild_StandsBeneathItsParentOnly()
    {
        var rows = Nest(FeatureRank, Features, Stories, Tasks);

        Assert.Equal("F8[F23] F6[US2[BUG5 T3] BUG4]", Shape(rows));
        Assert.Equal([Feature8, Feature6], rows.Select(row => row.ItemId));
    }

    // US-2 · "«Истории» rows open to their bugs and tasks": MSK-US-2, MSK-BUG-4, MSK-US-22 at the top (their parents
    // are above the page), MSK-BUG-5 and MSK-TASK-3 beneath MSK-US-2.
    [Fact]
    public void Nest_Stories_OpenToTheirBugsAndTasks()
    {
        Assert.Equal("US2[BUG5 T3] BUG4 US22", Shape(Nest(StoryRank, Stories, Tasks)));
    }

    // US-2 · "Another team's children stand beneath no row": an entry whose parent is no entry — another team's item,
    // or one in «Удалена», which the set never holds — nests nowhere; one of a lower level is then not on the page,
    // one of the page's level is a top-level row.
    [Fact]
    public void Nest_AParentOutsideTheSet_NestsNothing()
    {
        BacklogTreeEntry[] features = [new(Feature23, 23, null, Feature21, FeatureRank)];
        BacklogTreeEntry[] tasks = [new(Task3, 3, null, Story2, TaskRank), new(Task7, 7, null, Feature21, TaskRank)];

        Assert.Equal("E1 E20", Shape(Nest(EpicRank, Epics, features, tasks)));
        Assert.Equal("F23", Shape(Nest(FeatureRank, features, tasks)));
    }

    // US-2 · "A child in «Удалена» frees its parent's place": MSK-BUG-5 left the set, MSK-US-2 holds MSK-TASK-3 only.
    [Fact]
    public void Nest_AChildLeavingTheSet_LeavesItsParentsOtherChildren()
    {
        BacklogTreeEntry[] stories = [.. Stories.Where(entry => entry.ItemId != Bug5)];

        Assert.Equal("US2[T3] BUG4 US22", Shape(Nest(StoryRank, stories, Tasks)));
    }

    // BR-T-6: within a level the children follow its backlog's order — placed ones by place, then by number; tasks,
    // which have no places, by number.
    [Fact]
    public void Nest_ChildrenOfALevel_InItsStackRank_TasksByNumber()
    {
        var task9 = Guid.CreateVersion7();
        BacklogTreeEntry[] features = [new(Feature6, 6, null, Epic1, FeatureRank), new(Feature8, 8, null, Epic1, FeatureRank), new(Feature23, 23, 1, Epic1, FeatureRank)];
        BacklogTreeEntry[] tasks = [new(task9, 9, null, Epic1, TaskRank), new(Task3, 3, null, Epic1, TaskRank)];

        var epic = Nest(EpicRank, Epics, features, tasks)[0];

        Assert.Equal([Feature23, Feature6, Feature8, Task3, task9], epic.Children.Select(row => row.ItemId));
    }

    // BR-H-2 on the read: a parent of a lower level than its child (no write makes one) nests nothing; the child of
    // the page's level is a top-level row, and nothing shows twice.
    [Fact]
    public void Nest_AParentOfALowerLevel_NestsNothing()
    {
        BacklogTreeEntry[] features = [new(Feature6, 6, null, Story2, FeatureRank)];
        BacklogTreeEntry[] stories = [new(Story2, 2, null, null, StoryRank)];

        Assert.Equal("F6", Shape(Nest(FeatureRank, features, stories)));
    }

    // An item shows at most once: a stored cycle (no parent write makes one) stands beneath itself and is not reached;
    // an entry named as its own parent is no child of itself.
    [Fact]
    public void Nest_AStoredCycle_ShowsNowhere_ASelfLinkIsTopLevel()
    {
        BacklogTreeEntry[] cycle = [new(Feature6, 6, null, Feature8, FeatureRank), new(Feature8, 8, null, Feature6, FeatureRank)];
        BacklogTreeEntry[] self = [new(Feature23, 23, null, Feature23, FeatureRank)];

        Assert.Equal("F23", Shape(Nest(FeatureRank, cycle, self)));
    }

    // D-9 bounds a chain of parents at 64 items on the write side (BR-T-4, `workitems.link.depth_exceeded`), so the
    // 10 000-link chain below stands for one stored before the bound, which no stand holds: Nest walks it
    // iteratively and nests it to its end without exhausting the stack, bound or no bound.
    [Fact]
    public void Nest_AVeryDeepChain_NestsToItsEnd()
    {
        var entries = new List<BacklogTreeEntry> { new(Story2, 2, null, null, StoryRank) };
        var parent = Story2;

        for (var number = 100; number < 10_100; number++)
        {
            var id = Guid.CreateVersion7();
            entries.Add(new BacklogTreeEntry(id, number, null, parent, TaskRank));
            parent = id;
        }

        var depth = 0;

        for (var row = BacklogTree.Nest(StoryRank, entries).Single(); row.Children.Count > 0; row = row.Children.Single())
        {
            depth++;
        }

        Assert.Equal(10_000, depth);
    }

    // feat-039 BR-PB-3, BR-PB-4: on a project's page the entries of the levels that have a project order give their creation time, so
    // the unplaced rows of a level follow the placed ones by creation time before the number; the tasks give none and stand by
    // number (feat-027 BR-T-6). F23 numbered last, created first, leads the unplaced features; T9 created first still follows T3.
    [Fact]
    public void Nest_ProjectEntries_UnplacedByCreationTime_TasksByNumber()
    {
        var task9 = Guid.CreateVersion7();
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        BacklogTreeEntry[] features =
        [
            new(Feature6, 6, null, Epic1, FeatureRank, ten.AddMinutes(2)),
            new(Feature8, 8, null, Epic1, FeatureRank, ten.AddMinutes(1)),
            new(Feature23, 23, null, Epic1, FeatureRank, ten),
        ];
        BacklogTreeEntry[] tasks = [new(task9, 9, null, Epic1, TaskRank), new(Task3, 3, null, Epic1, TaskRank)];

        var epic = Nest(EpicRank, Epics, features, tasks)[0];

        Assert.Equal([Feature23, Feature8, Feature6, Task3, task9], epic.Children.Select(row => row.ItemId));
    }

    // An empty set, and a set with nothing of the page's level, have no rows.
    [Fact]
    public void Nest_NothingOfThePagesLevel_NoRows()
    {
        Assert.Empty(BacklogTree.Nest(EpicRank, []));
        Assert.Empty(Nest(EpicRank, Features, Stories));
    }
}
