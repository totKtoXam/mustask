using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>
/// feat-022's fields per type (spec § The field table, BR-M3-30..BR-M3-38),
/// without the database or the endpoint: which fields each kind of type has,
/// the defaults, every reason a save's fields are refused for, and how a save
/// applies them.
/// </summary>
public sealed class WorkItemFieldsTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddHours(1);

    private static readonly WorkItemTypeStates Epic = Type("EPIC", AppearanceToken.Epic);
    private static readonly WorkItemTypeStates Feature = Type("FEAT", AppearanceToken.Feature);
    private static readonly WorkItemTypeStates Story = Type("US", AppearanceToken.Story);
    private static readonly WorkItemTypeStates Task = Type("TASK", AppearanceToken.Task);
    private static readonly WorkItemTypeStates Bug = Type("BUG", AppearanceToken.Bug);

    private static WorkItemTypeStates Type(string key, AppearanceToken appearance) =>
        new(
            Guid.CreateVersion7(),
            key,
            appearance,
            [
                new WorkItemTypeState(Guid.CreateVersion7(), 1, StateCategory.Proposed),
                new WorkItemTypeState(Guid.CreateVersion7(), 2, StateCategory.Completed),
                new WorkItemTypeState(Guid.CreateVersion7(), 3, StateCategory.Removed),
            ]);

    private static WorkItem New(WorkItemTypeStates type) =>
        WorkItem.Create(Guid.CreateVersion7(), 1, type, Guid.CreateVersion7(), "Элемент", Guid.CreateVersion7(), Created, "creator-sub");

    private static WorkItemTypeStates TypeOf(AppearanceToken kind) => kind switch
    {
        AppearanceToken.Epic => Epic,
        AppearanceToken.Feature => Feature,
        AppearanceToken.Story => Story,
        AppearanceToken.Task => Task,
        _ => Bug,
    };

    private static IReadOnlyList<WorkItemFieldReason> Check(WorkItem item, WorkItemTypeStates type, WorkItemFieldsEdit fields) =>
        item.CheckFields(fields, type);

    private static void AssertOnly(IReadOnlyList<WorkItemFieldReason> reasons, WorkItemField field, string code) =>
        Assert.Equal(new WorkItemFieldReason(field, code), Assert.Single(reasons));

    private static FieldEdit<T> Value<T>(T value) => new(value);

    // --- The field set (spec § The field table) -----------------------------------------------------------------------

    // spec § The field table, "Per type": Эпик and Фича 8, История 5, Задача 5, Баг 9 (D-6), exactly these.
    [Theory]
    [InlineData(AppearanceToken.Epic, "Priority,Risk,Effort,BusinessValue,TimeCriticality,StartDate,TargetDate,ValueArea")]
    [InlineData(AppearanceToken.Feature, "Priority,Risk,Effort,BusinessValue,TimeCriticality,StartDate,TargetDate,ValueArea")]
    [InlineData(AppearanceToken.Story, "Priority,Risk,StoryPoints,ValueArea,AcceptanceCriteria")]
    [InlineData(AppearanceToken.Task, "Priority,Activity,OriginalEstimate,RemainingWork,CompletedWork")]
    [InlineData(AppearanceToken.Bug, "Priority,StoryPoints,Severity,Activity,OriginalEstimate,RemainingWork,CompletedWork,ReproSteps,SystemInfo")]
    public void FieldSet_EachKind_HasExactlyTheTablesFields(AppearanceToken kind, string fields)
    {
        var expected = fields.Split(',').Select(Enum.Parse<WorkItemField>).Order();

        Assert.Equal(expected, WorkItemFieldSet.Of(kind).Order());
    }

    // --- Defaults (BR-M3-37, US-5 "A new item takes its type's defaults") ----------------------------------------------

    [Theory]
    [InlineData(AppearanceToken.Epic, null, WorkItemValueArea.Business)]
    [InlineData(AppearanceToken.Feature, null, WorkItemValueArea.Business)]
    [InlineData(AppearanceToken.Story, null, WorkItemValueArea.Business)]
    [InlineData(AppearanceToken.Task, null, null)]
    [InlineData(AppearanceToken.Bug, 3, null)]
    public void Create_TakesItsTypesDefaults_EveryOtherFieldEmpty(AppearanceToken kind, int? severity, WorkItemValueArea? valueArea)
    {
        var item = New(TypeOf(kind));

        Assert.Equal(2, item.Priority);
        Assert.Equal(severity, item.Severity);
        Assert.Equal(valueArea, item.ValueArea);
        Assert.Equal(kind == AppearanceToken.Story ? (IReadOnlyList<AcceptanceCriterion>)[] : null, item.AcceptanceCriteria);
        Assert.Equal(kind == AppearanceToken.Bug ? string.Empty : null, item.ReproSteps);
        Assert.Equal(kind == AppearanceToken.Bug ? string.Empty : null, item.SystemInfo);
        Assert.All(
            new object?[]
            {
                item.Risk, item.StoryPoints, item.Effort, item.BusinessValue, item.TimeCriticality, item.StartDate, item.TargetDate,
                item.Activity, item.OriginalEstimate, item.RemainingWork, item.CompletedWork,
            },
            Assert.Null);
        Assert.Equal(Created, item.ChangedAt);
    }

    // --- Not on the type (BR-M3-30) -------------------------------------------------------------------------------------

    // US-5 · "A field of another type is refused", US-4 · "The field cannot be written onto another type": refused even
    // when the value is empty, and nothing else is checked on that field.
    [Fact]
    public void CheckFields_AFieldTheTypeLacks_IsFieldNotOnType_EvenEmpty()
    {
        AssertOnly(Check(New(Task), Task, new WorkItemFieldsEdit { StoryPoints = Value<decimal?>(3) }), WorkItemField.StoryPoints, WorkItemErrorCodes.FieldNotOnType);
        AssertOnly(Check(New(Story), Story, new WorkItemFieldsEdit { OriginalEstimate = Value<decimal?>(8) }), WorkItemField.OriginalEstimate, WorkItemErrorCodes.FieldNotOnType);
        AssertOnly(Check(New(Bug), Bug, new WorkItemFieldsEdit { ValueArea = Value<string?>("Business") }), WorkItemField.ValueArea, WorkItemErrorCodes.FieldNotOnType);
        AssertOnly(Check(New(Epic), Epic, new WorkItemFieldsEdit { Severity = Value<int?>(1) }), WorkItemField.Severity, WorkItemErrorCodes.FieldNotOnType);
        AssertOnly(Check(New(Task), Task, new WorkItemFieldsEdit { StartDate = Value<DateOnly?>(new DateOnly(2026, 11, 1)) }), WorkItemField.StartDate, WorkItemErrorCodes.FieldNotOnType);
        AssertOnly(Check(New(Task), Task, new WorkItemFieldsEdit { AcceptanceCriteria = [new AcceptanceCriterion("Готово", "Когда", "Тогда")] }), WorkItemField.AcceptanceCriteria, WorkItemErrorCodes.FieldNotOnType);
        AssertOnly(Check(New(Story), Story, new WorkItemFieldsEdit { ReproSteps = "" }), WorkItemField.ReproSteps, WorkItemErrorCodes.FieldNotOnType);
        AssertOnly(Check(New(Epic), Epic, new WorkItemFieldsEdit { SystemInfo = "Windows" }), WorkItemField.SystemInfo, WorkItemErrorCodes.FieldNotOnType);
        AssertOnly(Check(New(Feature), Feature, new WorkItemFieldsEdit { Activity = Value<string?>(null) }), WorkItemField.Activity, WorkItemErrorCodes.FieldNotOnType);
        AssertOnly(Check(New(Story), Story, new WorkItemFieldsEdit { Effort = Value<decimal?>(-5) }), WorkItemField.Effort, WorkItemErrorCodes.FieldNotOnType);
    }

    // --- The values (BR-M3-31, BR-M3-32, BR-M3-35, BR-M3-36) -------------------------------------------------------------

    // US-5 · "A priority outside 1..4 is refused"; US-3 · "A value outside the list is refused" (BR-M3-31).
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void CheckFields_PriorityOutsideOneToFour_IsValueNotInList(int priority) =>
        AssertOnly(Check(New(Task), Task, new WorkItemFieldsEdit { Priority = Value<int?>(priority) }), WorkItemField.Priority, WorkItemErrorCodes.ValueNotInList);

    [Fact]
    public void CheckFields_ListKeysOutsideTheirLists_AreValueNotInList()
    {
        var reasons = Check(New(Bug), Bug, new WorkItemFieldsEdit { Severity = Value<int?>(5), Activity = Value<string?>("development") });
        var epicReasons = Check(New(Epic), Epic, new WorkItemFieldsEdit { Risk = Value<int?>(4), ValueArea = Value<string?>("1") });

        Assert.Equal(
            new WorkItemFieldReason[] { new(WorkItemField.Severity, WorkItemErrorCodes.ValueNotInList), new(WorkItemField.Activity, WorkItemErrorCodes.ValueNotInList) },
            reasons);
        Assert.Equal(
            new WorkItemFieldReason[] { new(WorkItemField.Risk, WorkItemErrorCodes.ValueNotInList), new(WorkItemField.ValueArea, WorkItemErrorCodes.ValueNotInList) },
            epicReasons);
    }

    [Fact]
    public void CheckFields_EveryKeyOfTheLists_IsAccepted()
    {
        foreach (var activity in new[] { "Deployment", "Design", "Development", "Documentation", "Requirements", "Testing" })
        {
            Assert.Empty(Check(New(Task), Task, new WorkItemFieldsEdit { Activity = Value<string?>(activity) }));
        }

        foreach (var valueArea in new[] { "Business", "Architectural" })
        {
            Assert.Empty(Check(New(Story), Story, new WorkItemFieldsEdit { ValueArea = Value<string?>(valueArea) }));
        }

        foreach (var key in new[] { 1, 2, 3, 4 })
        {
            Assert.Empty(Check(New(Bug), Bug, new WorkItemFieldsEdit { Priority = Value<int?>(key), Severity = Value<int?>(key) }));
        }

        foreach (var risk in new[] { 1, 2, 3 })
        {
            Assert.Empty(Check(New(Feature), Feature, new WorkItemFieldsEdit { Risk = Value<int?>(risk) }));
        }
    }

    // US-2 · "An invalid number is refused, not rounded"; US-3 · "Hours are refused beyond two decimals" (BR-M3-32).
    [Theory]
    [InlineData("-1", WorkItemErrorCodes.NumberOutOfRange)]
    [InlineData("10000", WorkItemErrorCodes.NumberOutOfRange)]
    [InlineData("9999.01", WorkItemErrorCodes.NumberOutOfRange)]
    [InlineData("-0.001", WorkItemErrorCodes.NumberOutOfRange)]
    [InlineData("2.555", WorkItemErrorCodes.NumberTooPrecise)]
    [InlineData("1.333", WorkItemErrorCodes.NumberTooPrecise)]
    [InlineData("0", null)]
    [InlineData("9999", null)]
    [InlineData("13.5", null)]
    [InlineData("2.50", null)]
    public void ValidateDecimal_RangeThenPrecision(string text, string? code) =>
        Assert.Equal(code, WorkItemFieldRules.ValidateDecimal(decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("2.5", WorkItemErrorCodes.IntegerRequired)]
    [InlineData("10000.5", WorkItemErrorCodes.NumberOutOfRange)]
    [InlineData("-1", WorkItemErrorCodes.NumberOutOfRange)]
    [InlineData("40", null)]
    [InlineData("40.0", null)]
    [InlineData("0", null)]
    public void ValidateInteger_RangeThenWhole(string text, string? code) =>
        Assert.Equal(code, WorkItemFieldRules.ValidateInteger(decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture)));

    // US-2 · "A date outside 2000–2099 is refused" (BR-M3-35).
    [Theory]
    [InlineData(1999, 12, 31, WorkItemErrorCodes.DateOutOfRange)]
    [InlineData(2100, 1, 1, WorkItemErrorCodes.DateOutOfRange)]
    [InlineData(1, 1, 1, WorkItemErrorCodes.DateOutOfRange)]
    [InlineData(2000, 1, 1, null)]
    [InlineData(2099, 12, 31, null)]
    public void ValidateDate_From2000To2099(int year, int month, int day, string? code) =>
        Assert.Equal(code, WorkItemFieldRules.ValidateDate(new DateOnly(year, month, day)));

    // US-4 · "The length limit holds" (BR-M3-36): 100 000 characters are accepted, one more is refused.
    [Fact]
    public void CheckFields_TextOver100000_IsTextTooLong_100000IsAccepted()
    {
        var bug = New(Bug);

        Assert.Empty(Check(bug, Bug, new WorkItemFieldsEdit { ReproSteps = new string('ж', 100_000), SystemInfo = "" }));
        AssertOnly(Check(bug, Bug, new WorkItemFieldsEdit { SystemInfo = new string('ж', 100_001) }), WorkItemField.SystemInfo, WorkItemErrorCodes.TextTooLong);
        AssertOnly(Check(New(Story), Story, new WorkItemFieldsEdit { AcceptanceCriteria = [new AcceptanceCriterion(new string('ж', 100_001), "", "")] }), WorkItemField.AcceptanceCriteria, WorkItemErrorCodes.TextTooLong);
    }

    // US-1 · "One refused value stores nothing": every field's reason at once, one a field.
    [Fact]
    public void CheckFields_SeveralFaults_EveryReasonAtOnce()
    {
        var reasons = Check(New(Feature), Feature, new WorkItemFieldsEdit
        {
            Risk = Value<int?>(1),
            BusinessValue = Value<decimal?>(10_000),
            Effort = Value<decimal?>(2.555m),
            Severity = Value<int?>(1),
        });

        Assert.Equal(
            new WorkItemFieldReason[]
            {
                new(WorkItemField.Effort, WorkItemErrorCodes.NumberTooPrecise),
                new(WorkItemField.BusinessValue, WorkItemErrorCodes.NumberOutOfRange),
                new(WorkItemField.Severity, WorkItemErrorCodes.FieldNotOnType),
            },
            reasons);
    }

    // --- The dates' order (BR-M3-35, OQ-3 (a), US-2 "The target date may not precede the start date") ------------------

    [Fact]
    public void CheckFields_StartAfterTheStoredTarget_IsTargetBeforeStart_OnTheTarget()
    {
        var epic = New(Epic);
        epic.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { TargetDate = Value<DateOnly?>(new DateOnly(2027, 3, 31)) }), Epic, Later);

        var reasons = Check(epic, Epic, new WorkItemFieldsEdit { StartDate = Value<DateOnly?>(new DateOnly(2027, 4, 1)) });

        AssertOnly(reasons, WorkItemField.TargetDate, WorkItemErrorCodes.TargetBeforeStart);
    }

    [Fact]
    public void CheckFields_DatesInOrder_OrAlone_OrCleared_AreAccepted()
    {
        var epic = New(Epic);
        epic.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { TargetDate = Value<DateOnly?>(new DateOnly(2027, 3, 31)) }), Epic, Later);

        // Both in one save, in order.
        Assert.Empty(Check(epic, Epic, new WorkItemFieldsEdit
        {
            StartDate = Value<DateOnly?>(new DateOnly(2027, 4, 1)),
            TargetDate = Value<DateOnly?>(new DateOnly(2027, 6, 30)),
        }));

        // The same day, the target cleared with a later start, a start alone on an item with no target.
        Assert.Empty(Check(epic, Epic, new WorkItemFieldsEdit { StartDate = Value<DateOnly?>(new DateOnly(2027, 3, 31)) }));
        Assert.Empty(Check(epic, Epic, new WorkItemFieldsEdit { StartDate = Value<DateOnly?>(new DateOnly(2027, 4, 1)), TargetDate = Value<DateOnly?>(null) }));
        Assert.Empty(Check(New(Epic), Epic, new WorkItemFieldsEdit { StartDate = Value<DateOnly?>(new DateOnly(2027, 4, 1)) }));
    }

    [Fact]
    public void CheckFields_TargetBeforeStart_InOneSave_IsRefused_UnlessADateHasItsOwnReason()
    {
        var feature = New(Feature);

        AssertOnly(
            Check(feature, Feature, new WorkItemFieldsEdit
            {
                StartDate = Value<DateOnly?>(new DateOnly(2027, 4, 1)),
                TargetDate = Value<DateOnly?>(new DateOnly(2027, 3, 31)),
            }),
            WorkItemField.TargetDate,
            WorkItemErrorCodes.TargetBeforeStart);

        AssertOnly(
            Check(feature, Feature, new WorkItemFieldsEdit
            {
                StartDate = Value<DateOnly?>(new DateOnly(2100, 1, 1)),
                TargetDate = Value<DateOnly?>(new DateOnly(2027, 3, 31)),
            }),
            WorkItemField.StartDate,
            WorkItemErrorCodes.DateOutOfRange);
    }

    // --- Applying a save (BR-M3-31, BR-M3-34, BR-M3-38) ------------------------------------------------------------------

    // US-2 · "An epic's planning and classification are saved".
    [Fact]
    public void Save_EpicsFields_AreStored_ChangedAtMoves()
    {
        var epic = New(Epic);

        Assert.True(epic.Save(
            new WorkItemEdit(Fields: new WorkItemFieldsEdit
            {
                Risk = Value<int?>(1),
                Effort = Value<decimal?>(13.5m),
                BusinessValue = Value<decimal?>(40),
                TimeCriticality = Value<decimal?>(8),
                StartDate = Value<DateOnly?>(new DateOnly(2026, 11, 1)),
                TargetDate = Value<DateOnly?>(new DateOnly(2027, 3, 31)),
                ValueArea = Value<string?>("Architectural"),
            }),
            Epic,
            Later));

        Assert.Equal(
            (1, 13.5m, 40, 8m, new DateOnly(2026, 11, 1), new DateOnly(2027, 3, 31), WorkItemValueArea.Architectural),
            (epic.Risk!.Value, epic.Effort!.Value, epic.BusinessValue!.Value, epic.TimeCriticality!.Value, epic.StartDate!.Value, epic.TargetDate!.Value, epic.ValueArea!.Value));
        Assert.Equal(Later, epic.ChangedAt);
    }

    // US-2 · "Risk can be emptied, the value area cannot"; US-3 · "Severity cannot be emptied"; US-5 · "Приоритет cannot be
    // emptied" (BR-M3-31): an emptied optional field is null, an emptied always-set one takes its default.
    [Fact]
    public void Save_Emptied_OptionalIsNull_AlwaysSetTakesItsDefault()
    {
        var feature = New(Feature);
        var bug = New(Bug);
        feature.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { Risk = Value<int?>(2), Priority = Value<int?>(4), ValueArea = Value<string?>("Architectural") }), Feature, Later);
        bug.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { Severity = Value<int?>(1), Activity = Value<string?>("Testing") }), Bug, Later);

        Assert.True(feature.Save(
            new WorkItemEdit(Fields: new WorkItemFieldsEdit { Risk = Value<int?>(null), Priority = Value<int?>(null), ValueArea = Value<string?>(null) }),
            Feature,
            Later.AddHours(1)));
        Assert.True(bug.Save(
            new WorkItemEdit(Fields: new WorkItemFieldsEdit { Severity = Value<int?>(null), Activity = Value<string?>(null) }),
            Bug,
            Later.AddHours(1)));

        Assert.Equal((null, 2, WorkItemValueArea.Business), (feature.Risk, feature.Priority, feature.ValueArea));
        Assert.Equal((3, (WorkItemActivity?)null), (bug.Severity, bug.Activity));
    }

    // US-2 · "A number field may be emptied, and 0 is kept as 0"; US-3 · "An empty estimate is not zero" (BR-M3-32).
    [Fact]
    public void Save_ZeroIsAValue_EmptyIsNull()
    {
        var task = New(Task);

        task.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { RemainingWork = Value<decimal?>(0) }), Task, Later);
        Assert.Equal(0m, task.RemainingWork);

        task.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { RemainingWork = Value<decimal?>(null) }), Task, Later.AddHours(1));
        Assert.Null(task.RemainingWork);
    }

    // US-1 · "Nothing to save" (BR-M3-38): values equal to the stored ones — 2.50 against 2.5 included — change nothing.
    [Fact]
    public void Save_EqualValues_ChangeNothing_ChangedAtStays()
    {
        var story = New(Story);
        story.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { StoryPoints = Value<decimal?>(2.5m), AcceptanceCriteria = [new AcceptanceCriterion("Вход", "Пароль", "Успех")] }), Story, Later);

        Assert.False(story.Save(
            new WorkItemEdit(Fields: new WorkItemFieldsEdit
            {
                StoryPoints = Value<decimal?>(2.50m),
                AcceptanceCriteria = [new AcceptanceCriterion("Вход", "Пароль", "Успех")],
                Priority = Value<int?>(null),
                ValueArea = Value<string?>("Business"),
                Risk = Value<int?>(null),
            }),
            Story,
            Later.AddHours(1)));
        Assert.Equal(Later, story.ChangedAt);
    }

    // US-3 · "No field changes another" and "Closing a task leaves its remaining work as entered" (BR-M3-34).
    [Fact]
    public void Save_NoFieldChangesAnother_AndAStateChangeChangesNone()
    {
        var task = New(Task);
        task.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit
        {
            OriginalEstimate = Value<decimal?>(8),
            RemainingWork = Value<decimal?>(5),
            CompletedWork = Value<decimal?>(3),
        }), Task, Later);

        task.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { CompletedWork = Value<decimal?>(6) }), Task, Later.AddHours(1));
        task.Save(new WorkItemEdit(StateId: Task.States.Single(state => state.Category == StateCategory.Completed).Id), Task, Later.AddHours(2));

        Assert.Equal((8m, 5m, 6m), (task.OriginalEstimate!.Value, task.RemainingWork!.Value, task.CompletedWork!.Value));
    }

    // The handler checks the fields first; a save carrying unchecked ones is a defect, and changes nothing.
    [Fact]
    public void Save_UncheckedFields_IsADefect_ItemUnchanged()
    {
        var task = New(Task);

        Assert.Throws<ArgumentException>(() => task.Save(
            new WorkItemEdit(Title: "Иное", Fields: new WorkItemFieldsEdit { Priority = Value<int?>(9) }),
            Task,
            Later));
        Assert.Equal(("Элемент", 2, Created), (task.Title, task.Priority, task.ChangedAt));
    }

    // --- Structured Acceptance Criteria (R1, R4) -------------------------------------------------------------------

    [Fact]
    public void CheckFields_ValidScenarios_AreAccepted()
    {
        var story = New(Story);
        var scenarios = new[]
        {
            new AcceptanceCriterion("Given context", "When action", "Then result"),
            new AcceptanceCriterion("Given another context", "When another action", "Then another result"),
        };

        var reasons = Check(story, Story, new WorkItemFieldsEdit { AcceptanceCriteria = scenarios });

        Assert.Empty(reasons);
    }

    [Fact]
    public void CheckFields_EmptyScenarioListOnStory_IsAccepted()
    {
        var story = New(Story);
        var reasons = Check(story, Story, new WorkItemFieldsEdit { AcceptanceCriteria = [] });

        Assert.Empty(reasons);
    }

    [Theory]
    [InlineData(AppearanceToken.Epic)]
    [InlineData(AppearanceToken.Feature)]
    [InlineData(AppearanceToken.Task)]
    [InlineData(AppearanceToken.Bug)]
    public void CheckFields_AcceptanceCriteriaOnNonStoryTypes_IsFieldNotOnType(AppearanceToken kind)
    {
        var type = TypeOf(kind);
        var item = New(type);
        var reasons = Check(item, type, new WorkItemFieldsEdit
        {
            AcceptanceCriteria = [new AcceptanceCriterion("Given", "When", "Then")],
        });

        AssertOnly(reasons, WorkItemField.AcceptanceCriteria, WorkItemErrorCodes.FieldNotOnType);
    }

    [Fact]
    public void CheckFields_ScenarioStepOver100000_IsTextTooLong()
    {
        var story = New(Story);
        var longText = new string('a', 100_001);

        AssertOnly(
            Check(story, Story, new WorkItemFieldsEdit { AcceptanceCriteria = [new AcceptanceCriterion(longText, "When", "Then")] }),
            WorkItemField.AcceptanceCriteria,
            WorkItemErrorCodes.TextTooLong);

        AssertOnly(
            Check(story, Story, new WorkItemFieldsEdit { AcceptanceCriteria = [new AcceptanceCriterion("Given", longText, "Then")] }),
            WorkItemField.AcceptanceCriteria,
            WorkItemErrorCodes.TextTooLong);

        AssertOnly(
            Check(story, Story, new WorkItemFieldsEdit { AcceptanceCriteria = [new AcceptanceCriterion("Given", "When", longText)] }),
            WorkItemField.AcceptanceCriteria,
            WorkItemErrorCodes.TextTooLong);
    }

    [Fact]
    public void Save_AcceptanceCriteria_UpdatesValueAndChangedAt()
    {
        var story = New(Story);
        var scenarios = new[]
        {
            new AcceptanceCriterion("Given authenticated", "When submits", "Then saved"),
        };

        var changed = story.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = scenarios }), Story, Later);

        Assert.True(changed);
        Assert.Equal(Later, story.ChangedAt);
        Assert.Equal(scenarios, story.AcceptanceCriteria);
    }
}
