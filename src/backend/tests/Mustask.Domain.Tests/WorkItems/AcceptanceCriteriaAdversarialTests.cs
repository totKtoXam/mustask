using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>
/// Adversarial stress tests for structured Acceptance Criteria on User Stories (M1).
/// Validates boundary conditions, nullability invariants, extreme payload sizes,
/// non-story type rejection, and domain mutation detection.
/// </summary>
public sealed class AcceptanceCriteriaAdversarialTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddHours(2);

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
        WorkItem.Create(Guid.CreateVersion7(), 1, type, Guid.CreateVersion7(), "Adversarial Item", Guid.CreateVersion7(), Created, "challenger-sub");

    // 1. Empty criteria list [] on User Story vs null
    [Fact]
    public void Story_InitialState_HasEmptyListNotNuLL()
    {
        var story = New(Story);
        Assert.NotNull(story.AcceptanceCriteria);
        Assert.Empty(story.AcceptanceCriteria);
    }

    [Fact]
    public void Story_CheckFields_EmptyListAndNull_BothAccepted()
    {
        var story = New(Story);
        Assert.Empty(story.CheckFields(new WorkItemFieldsEdit { AcceptanceCriteria = [] }, Story));
        Assert.Empty(story.CheckFields(new WorkItemFieldsEdit { AcceptanceCriteria = null }, Story));
    }

    [Fact]
    public void Story_Save_EmptyListWhenAlreadyEmpty_DoesNotChange()
    {
        var story = New(Story);
        var changed = story.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = [] }), Story, Later);
        Assert.False(changed);
        Assert.Equal(Created, story.ChangedAt);
        Assert.NotNull(story.AcceptanceCriteria);
        Assert.Empty(story.AcceptanceCriteria);
    }

    [Fact]
    public void Story_Save_NullField_LeavesExistingCriteriaUntouched()
    {
        var story = New(Story);
        var original = new AcceptanceCriterion[] { new("Given G", "When W", "Then T") };
        story.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = original }), Story, Later);
        Assert.Equal(original, story.AcceptanceCriteria);

        var changed = story.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = null }), Story, Later.AddHours(1));
        Assert.False(changed);
        Assert.Equal(original, story.AcceptanceCriteria);
    }

    [Fact]
    public void Story_Save_EmptyListClearsExistingScenarios()
    {
        var story = New(Story);
        var original = new AcceptanceCriterion[] { new("Given G", "When W", "Then T") };
        story.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = original }), Story, Later);
        Assert.Single(story.AcceptanceCriteria!);

        var changed = story.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = [] }), Story, Later.AddHours(1));
        Assert.True(changed);
        Assert.NotNull(story.AcceptanceCriteria);
        Assert.Empty(story.AcceptanceCriteria);
    }

    // 2. Missing fields / null fields in scenario
    [Fact]
    public void Scenario_NullConstructorArgs_DefaultToEmptyStrings()
    {
        var criterion = new AcceptanceCriterion(null!, null!, null!);
        Assert.Equal(string.Empty, criterion.Given);
        Assert.Equal(string.Empty, criterion.When);
        Assert.Equal(string.Empty, criterion.Then);

        var criterion2 = new AcceptanceCriterion("G", null!, "T");
        Assert.Equal("G", criterion2.Given);
        Assert.Equal(string.Empty, criterion2.When);
        Assert.Equal("T", criterion2.Then);
    }

    [Fact]
    public void Scenario_RecordEquality_CoalescesNullToEmpty()
    {
        var c1 = new AcceptanceCriterion(null!, null!, null!);
        var c2 = new AcceptanceCriterion(string.Empty, string.Empty, string.Empty);
        Assert.Equal(c1, c2);
        Assert.Equal(c1.GetHashCode(), c2.GetHashCode());
    }

    [Fact]
    public void ValidateAcceptanceCriteria_ListContainingNullCriterion_DoesNotThrow()
    {
        var criteriaWithNull = new AcceptanceCriterion[] { null!, new("G", "W", "T") };
        var result = WorkItemFieldRules.ValidateAcceptanceCriteria(criteriaWithNull);
        Assert.Null(result);
    }

    [Fact]
    public void ValidateAcceptanceCriteria_NullList_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => WorkItemFieldRules.ValidateAcceptanceCriteria(null!));
    }

    // 3. Extremely long text (>100,000 characters) triggering TextTooLong
    [Fact]
    public void TextLengthBoundary_Exactly100000Chars_IsAccepted()
    {
        var story = New(Story);
        var exactLengthString = new string('ж', 100_000);
        var scenarios = new[]
        {
            new AcceptanceCriterion(exactLengthString, exactLengthString, exactLengthString),
        };

        var reasons = story.CheckFields(new WorkItemFieldsEdit { AcceptanceCriteria = scenarios }, Story);
        Assert.Empty(reasons);
    }

    [Theory]
    [InlineData("Given")]
    [InlineData("When")]
    [InlineData("Then")]
    public void TextLengthBoundary_100001Chars_TriggerTextTooLong(string field)
    {
        var story = New(Story);
        var longString = new string('a', 100_001);
        var scenario = field switch
        {
            "Given" => new AcceptanceCriterion(longString, "W", "T"),
            "When" => new AcceptanceCriterion("G", longString, "T"),
            _ => new AcceptanceCriterion("G", "W", longString),
        };

        var reasons = story.CheckFields(new WorkItemFieldsEdit { AcceptanceCriteria = [scenario] }, Story);
        var reason = Assert.Single(reasons);
        Assert.Equal(WorkItemField.AcceptanceCriteria, reason.Field);
        Assert.Equal(WorkItemErrorCodes.TextTooLong, reason.Code);
    }

    [Fact]
    public void TextLengthBoundary_FiftyScenarios_WithLastScenarioExceeding_TriggersTextTooLong()
    {
        var story = New(Story);
        var list = new List<AcceptanceCriterion>();
        for (var i = 0; i < 49; i++)
        {
            list.Add(new AcceptanceCriterion($"G{i}", $"W{i}", $"T{i}"));
        }
        list.Add(new AcceptanceCriterion("G49", "W49", new string('z', 200_000)));

        var reasons = story.CheckFields(new WorkItemFieldsEdit { AcceptanceCriteria = list }, Story);
        var reason = Assert.Single(reasons);
        Assert.Equal(WorkItemField.AcceptanceCriteria, reason.Field);
        Assert.Equal(WorkItemErrorCodes.TextTooLong, reason.Code);
    }

    // 4. Submission on non-story types (Task, Bug, Epic, Feature) triggering FieldNotOnType
    [Theory]
    [InlineData(AppearanceToken.Epic)]
    [InlineData(AppearanceToken.Feature)]
    [InlineData(AppearanceToken.Task)]
    [InlineData(AppearanceToken.Bug)]
    public void NonStoryTypes_InitialAcceptanceCriteria_IsNull(AppearanceToken kind)
    {
        var type = kind switch
        {
            AppearanceToken.Epic => Epic,
            AppearanceToken.Feature => Feature,
            AppearanceToken.Task => Task,
            _ => Bug,
        };
        var item = New(type);
        Assert.Null(item.AcceptanceCriteria);
    }

    [Theory]
    [InlineData(AppearanceToken.Epic)]
    [InlineData(AppearanceToken.Feature)]
    [InlineData(AppearanceToken.Task)]
    [InlineData(AppearanceToken.Bug)]
    public void NonStoryTypes_SubmittingNonEmptyOrEmptyList_TriggersFieldNotOnType(AppearanceToken kind)
    {
        var type = kind switch
        {
            AppearanceToken.Epic => Epic,
            AppearanceToken.Feature => Feature,
            AppearanceToken.Task => Task,
            _ => Bug,
        };
        var item = New(type);

        // Submitting non-empty
        var reasonsNonEmpty = item.CheckFields(new WorkItemFieldsEdit
        {
            AcceptanceCriteria = [new AcceptanceCriterion("G", "W", "T")],
        }, type);
        var r1 = Assert.Single(reasonsNonEmpty);
        Assert.Equal(WorkItemField.AcceptanceCriteria, r1.Field);
        Assert.Equal(WorkItemErrorCodes.FieldNotOnType, r1.Code);

        // Submitting empty list [] on non-story type also triggers FieldNotOnType
        var reasonsEmpty = item.CheckFields(new WorkItemFieldsEdit
        {
            AcceptanceCriteria = [],
        }, type);
        var r2 = Assert.Single(reasonsEmpty);
        Assert.Equal(WorkItemField.AcceptanceCriteria, r2.Field);
        Assert.Equal(WorkItemErrorCodes.FieldNotOnType, r2.Code);

        // Submitting null (omitted) does not trigger FieldNotOnType
        var reasonsNull = item.CheckFields(new WorkItemFieldsEdit
        {
            AcceptanceCriteria = null,
        }, type);
        Assert.Empty(reasonsNull);
    }

    // 5. SequenceEqual and change detection
    [Fact]
    public void Save_IdenticalList_DoesNotUpdateChangedAt()
    {
        var story = New(Story);
        var scenarios = new AcceptanceCriterion[]
        {
            new("G1", "W1", "T1"),
            new("G2", "W2", "T2"),
        };

        story.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = scenarios }), Story, Created.AddMinutes(10));
        var markedTime = story.ChangedAt;

        // Save identical content
        var identicalList = new AcceptanceCriterion[]
        {
            new("G1", "W1", "T1"),
            new("G2", "W2", "T2"),
        };
        var changed = story.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = identicalList }), Story, Later);
        Assert.False(changed);
        Assert.Equal(markedTime, story.ChangedAt);
    }

    [Fact]
    public void Save_ReorderedList_IsDetectedAsChange()
    {
        var story = New(Story);
        story.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit
        {
            AcceptanceCriteria = [new("G1", "W1", "T1"), new("G2", "W2", "T2")],
        }), Story, Created.AddMinutes(10));

        // Save reordered
        var changed = story.Save(new WorkItemEdit(Fields: new WorkItemFieldsEdit
        {
            AcceptanceCriteria = [new("G2", "W2", "T2"), new("G1", "W1", "T1")],
        }), Story, Later);
        Assert.True(changed);
        Assert.Equal(Later, story.ChangedAt);
    }
}
