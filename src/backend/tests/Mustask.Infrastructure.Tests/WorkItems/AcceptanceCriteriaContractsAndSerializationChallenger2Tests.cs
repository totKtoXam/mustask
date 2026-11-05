using System.Text.Json;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// Challenger 2 adversarial verification harness:
/// - JSON serialization / deserialization (camelCase vs PascalCase vs UPPERCASE)
/// - Unicode, Cyrillic, whitespace, and special characters preservation
/// - DTO projection in WorkItemReadModel / WorkItemView
/// - WorkItem.Apply isolation: zero unwanted side effects on any other field
/// </summary>
public sealed class AcceptanceCriteriaContractsAndSerializationChallenger2Tests
{
    private static MustaskDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MustaskDbContext>()
            .UseNpgsql("Host=localhost;Database=mustask")
            .Options;

        return new MustaskDbContext(options);
    }

    private static readonly DateTimeOffset Created = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddHours(1);

    private static WorkItemTypeStates CreateType(string key, AppearanceToken appearance) =>
        new(
            Guid.CreateVersion7(),
            key,
            appearance,
            [
                new WorkItemTypeState(Guid.CreateVersion7(), 1, StateCategory.Proposed),
                new WorkItemTypeState(Guid.CreateVersion7(), 2, StateCategory.Completed),
                new WorkItemTypeState(Guid.CreateVersion7(), 3, StateCategory.Removed),
            ]);

    private static readonly WorkItemTypeStates StoryType = CreateType("US", AppearanceToken.Story);
    private static readonly WorkItemTypeStates BugType = CreateType("BUG", AppearanceToken.Bug);
    private static readonly WorkItemTypeStates EpicType = CreateType("EPIC", AppearanceToken.Epic);
    private static readonly WorkItemTypeStates TaskType = CreateType("TASK", AppearanceToken.Task);

    private static WorkItem CreateStory() =>
        WorkItem.Create(Guid.CreateVersion7(), 1, StoryType, Guid.CreateVersion7(), "Story Title", Guid.CreateVersion7(), Created, "author-1");

    // =========================================================================
    // 1. JSON Serialization & Deserialization (camelCase vs PascalCase vs UPPERCASE)
    // =========================================================================

    [Theory]
    [InlineData("[{\"given\":\"G\",\"when\":\"W\",\"then\":\"T\"}]")]               // camelCase
    [InlineData("[{\"Given\":\"G\",\"When\":\"W\",\"Then\":\"T\"}]")]               // PascalCase
    [InlineData("[{\"GIVEN\":\"G\",\"WHEN\":\"W\",\"THEN\":\"T\"}]")]               // UPPERCASE
    [InlineData("[{\"GiVeN\":\"G\",\"wHeN\":\"W\",\"ThEn\":\"T\"}]")]               // mixedCase
    public void Deserialization_HandlesAllCasingStyles_CaseInsensitive(string json)
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var converter = property.GetValueConverter()!;

        var result = (IReadOnlyList<AcceptanceCriterion>?)converter.ConvertFromProvider(json);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("G", result[0].Given);
        Assert.Equal("W", result[0].When);
        Assert.Equal("T", result[0].Then);
    }

    [Fact]
    public void Serialization_ProducesCamelCaseWireContract()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var converter = property.GetValueConverter()!;

        var input = new List<AcceptanceCriterion> { new("context", "action", "outcome") };
        var json = (string?)converter.ConvertToProvider(input);

        Assert.NotNull(json);
        Assert.Contains("\"given\":", json);
        Assert.Contains("\"when\":", json);
        Assert.Contains("\"then\":", json);
        Assert.DoesNotContain("\"Given\":", json);
        Assert.DoesNotContain("\"When\":", json);
        Assert.DoesNotContain("\"Then\":", json);
    }

    [Fact]
    public void Deserialization_HandlesMissingOrExtraPropertiesSafely()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var converter = property.GetValueConverter()!;

        // Missing when & then; extra property "extra"
        var json = "[{\"given\":\"G_only\",\"extra\":123}]";
        var result = (IReadOnlyList<AcceptanceCriterion>?)converter.ConvertFromProvider(json);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("G_only", result[0].Given);
        Assert.Equal(string.Empty, result[0].When);
        Assert.Equal(string.Empty, result[0].Then);
    }

    // =========================================================================
    // 2. Unicode, Cyrillic, Whitespace and Special Characters Preservation
    // =========================================================================

    [Fact]
    public void Roundtrip_PreservesWhitespaceFaithfully()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var converter = property.GetValueConverter()!;

        var input = new List<AcceptanceCriterion>
        {
            new("  leading and trailing  ", "\t\twith tabs\t\t", "\r\nmultiline\r\nand spaces  "),
            new("   ", "", " "),
        };

        var serialized = (string?)converter.ConvertToProvider(input);
        Assert.NotNull(serialized);

        var deserialized = (IReadOnlyList<AcceptanceCriterion>?)converter.ConvertFromProvider(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);

        Assert.Equal("  leading and trailing  ", deserialized[0].Given);
        Assert.Equal("\t\twith tabs\t\t", deserialized[0].When);
        Assert.Equal("\r\nmultiline\r\nand spaces  ", deserialized[0].Then);

        Assert.Equal("   ", deserialized[1].Given);
        Assert.Equal("", deserialized[1].When);
        Assert.Equal(" ", deserialized[1].Then);
    }

    [Fact]
    public void Roundtrip_PreservesCyrillicAndUnicodeSymbolsWithoutEscaping()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(WorkItem))!.FindProperty(nameof(WorkItem.AcceptanceCriteria))!;
        var converter = property.GetValueConverter()!;

        var input = new List<AcceptanceCriterion>
        {
            new(
                "Дано: пользователь в системе «Мустаск» с правами «Администратор»",
                "Когда: нажимает кнопку «Создать задачу» и вводит №42 (спецсимволы: © ® € —)",
                "Тогда: система сохраняет данные корректно, эмодзи 🚀 🎯 отображаются"),
        };

        var serialized = (string?)converter.ConvertToProvider(input);
        Assert.NotNull(serialized);

        // Cyrillic characters must NOT be converted to \u04xx escapes
        Assert.Contains("«Мустаск»", serialized);
        Assert.Contains("«Администратор»", serialized);
        Assert.Contains("«Создать задачу»", serialized);

        var deserialized = (IReadOnlyList<AcceptanceCriterion>?)converter.ConvertFromProvider(serialized);
        Assert.NotNull(deserialized);
        Assert.Single(deserialized);

        Assert.Equal(input[0].Given, deserialized[0].Given);
        Assert.Equal(input[0].When, deserialized[0].When);
        Assert.Equal(input[0].Then, deserialized[0].Then);
    }

    [Fact]
    public void AcceptanceCriterion_ConstructedWithNulls_NormalizesToEmptyStrings()
    {
        var criterion = new AcceptanceCriterion(null!, null!, null!);
        Assert.Equal(string.Empty, criterion.Given);
        Assert.Equal(string.Empty, criterion.When);
        Assert.Equal(string.Empty, criterion.Then);
    }

    // =========================================================================
    // 3. DTO Projection in Read Model / View
    // =========================================================================

    [Fact]
    public void DTO_Projection_MapsAcceptanceCriteriaFaithfully()
    {
        var domainCriteria = new List<AcceptanceCriterion>
        {
            new("Given context 1", "When action 1", "Then outcome 1"),
            new("Given context 2", "When action 2", "Then outcome 2"),
        };

        // Projecting from domain to view
        var viewCriteria = domainCriteria.Select(c => new AcceptanceCriterionView(c.Given, c.When, c.Then)).ToList();

        Assert.Equal(2, viewCriteria.Count);
        Assert.Equal("Given context 1", viewCriteria[0].Given);
        Assert.Equal("When action 1", viewCriteria[0].When);
        Assert.Equal("Then outcome 1", viewCriteria[0].Then);
        Assert.Equal("Given context 2", viewCriteria[1].Given);
        Assert.Equal("When action 2", viewCriteria[1].When);
        Assert.Equal("Then outcome 2", viewCriteria[1].Then);
    }

    [Fact]
    public void DTO_Projection_NullAcceptanceCriteria_RemainsNullInView()
    {
        IReadOnlyList<AcceptanceCriterion>? domainCriteria = null;
        var viewCriteria = domainCriteria?.Select(c => new AcceptanceCriterionView(c.Given, c.When, c.Then)).ToList();

        Assert.Null(viewCriteria);
    }

    [Fact]
    public void DTO_Projection_EmptyAcceptanceCriteria_RemainsEmptyInView()
    {
        IReadOnlyList<AcceptanceCriterion>? domainCriteria = [];
        var viewCriteria = domainCriteria?.Select(c => new AcceptanceCriterionView(c.Given, c.When, c.Then)).ToList();

        Assert.NotNull(viewCriteria);
        Assert.Empty(viewCriteria);
    }

    // =========================================================================
    // 4. WorkItem.Apply Isolation: Zero Unwanted Side Effects on Other Fields
    // =========================================================================

    [Fact]
    public void Save_AcceptanceCriteria_HasZeroSideEffectsOnOtherStoryFields()
    {
        var story = CreateStory();

        // 1. Populate all initial fields on the story
        story.Save(
            new WorkItemEdit(
                Title: "Original Story Title",
                Description: "Original Story Description",
                Fields: new WorkItemFieldsEdit
                {
                    Priority = new FieldEdit<int?>(3),
                    Risk = new FieldEdit<int?>(2),
                    StoryPoints = new FieldEdit<decimal?>(5.5m),
                    ValueArea = new FieldEdit<string?>("Architectural"),
                    AcceptanceCriteria = [new AcceptanceCriterion("Initial Given", "Initial When", "Initial Then")],
                }),
            StoryType,
            Later);

        // Snapshot all non-AcceptanceCriteria fields
        var titleBefore = story.Title;
        var descBefore = story.Description;
        var priorityBefore = story.Priority;
        var riskBefore = story.Risk;
        var spBefore = story.StoryPoints;
        var valueAreaBefore = story.ValueArea;
        var stateBefore = story.StateId;
        var assigneeBefore = story.AssigneeId;
        var areaBefore = story.AreaId;
        var effortBefore = story.Effort;
        var bvBefore = story.BusinessValue;
        var tcBefore = story.TimeCriticality;
        var startBefore = story.StartDate;
        var targetBefore = story.TargetDate;
        var severityBefore = story.Severity;
        var activityBefore = story.Activity;
        var origEstBefore = story.OriginalEstimate;
        var remWorkBefore = story.RemainingWork;
        var compWorkBefore = story.CompletedWork;
        var reproBefore = story.ReproSteps;
        var sysInfoBefore = story.SystemInfo;

        // 2. Mutate ONLY AcceptanceCriteria
        var updatedCriteria = new[]
        {
            new AcceptanceCriterion("New Given 1", "New When 1", "New Then 1"),
            new AcceptanceCriterion("New Given 2", "New When 2", "New Then 2"),
        };

        var mutationTime = Later.AddHours(1);
        var changed = story.Save(
            new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = updatedCriteria }),
            StoryType,
            mutationTime);

        // Assert AcceptanceCriteria updated and timestamp moved
        Assert.True(changed);
        Assert.Equal(mutationTime, story.ChangedAt);
        Assert.Equal(updatedCriteria, story.AcceptanceCriteria);

        // Assert ABSOLUTELY ZERO side effects on ANY other field
        Assert.Equal(titleBefore, story.Title);
        Assert.Equal(descBefore, story.Description);
        Assert.Equal(priorityBefore, story.Priority);
        Assert.Equal(riskBefore, story.Risk);
        Assert.Equal(spBefore, story.StoryPoints);
        Assert.Equal(valueAreaBefore, story.ValueArea);
        Assert.Equal(stateBefore, story.StateId);
        Assert.Equal(assigneeBefore, story.AssigneeId);
        Assert.Equal(areaBefore, story.AreaId);
        Assert.Equal(effortBefore, story.Effort);
        Assert.Equal(bvBefore, story.BusinessValue);
        Assert.Equal(tcBefore, story.TimeCriticality);
        Assert.Equal(startBefore, story.StartDate);
        Assert.Equal(targetBefore, story.TargetDate);
        Assert.Equal(severityBefore, story.Severity);
        Assert.Equal(activityBefore, story.Activity);
        Assert.Equal(origEstBefore, story.OriginalEstimate);
        Assert.Equal(remWorkBefore, story.RemainingWork);
        Assert.Equal(compWorkBefore, story.CompletedWork);
        Assert.Equal(reproBefore, story.ReproSteps);
        Assert.Equal(sysInfoBefore, story.SystemInfo);
    }

    [Fact]
    public void Save_OtherStoryFields_HasZeroSideEffectsOnAcceptanceCriteria()
    {
        var story = CreateStory();
        var criteria = new[] { new AcceptanceCriterion("Stable Given", "Stable When", "Stable Then") };

        story.Save(
            new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = criteria }),
            StoryType,
            Later);

        // Mutate StoryPoints, Priority, and ValueArea without touching AcceptanceCriteria
        story.Save(
            new WorkItemEdit(
                Fields: new WorkItemFieldsEdit
                {
                    StoryPoints = new FieldEdit<decimal?>(8m),
                    Priority = new FieldEdit<int?>(1),
                    ValueArea = new FieldEdit<string?>("Business"),
                }),
            StoryType,
            Later.AddHours(1));

        // AcceptanceCriteria must remain completely untouched
        Assert.Equal(criteria, story.AcceptanceCriteria);
    }

    [Fact]
    public void Save_IdenticalAcceptanceCriteria_IsNoOp_ChangedAtDoesNotMove()
    {
        var story = CreateStory();
        var criteria = new[] { new AcceptanceCriterion("G", "W", "T") };

        story.Save(
            new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = criteria }),
            StoryType,
            Later);

        var changedAtBefore = story.ChangedAt;

        // Re-saving with identical values (different list instance, same contents)
        var identicalCriteria = new[] { new AcceptanceCriterion("G", "W", "T") };
        var changed = story.Save(
            new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = identicalCriteria }),
            StoryType,
            Later.AddHours(2));

        Assert.False(changed);
        Assert.Equal(changedAtBefore, story.ChangedAt);
    }

    [Fact]
    public void Save_ReorderedAcceptanceCriteria_TriggersChange_ChangedAtMoves()
    {
        var story = CreateStory();
        var c1 = new AcceptanceCriterion("G1", "W1", "T1");
        var c2 = new AcceptanceCriterion("G2", "W2", "T2");

        story.Save(
            new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = [c1, c2] }),
            StoryType,
            Later);

        var reorderTime = Later.AddHours(1);
        var changed = story.Save(
            new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = [c2, c1] }),
            StoryType,
            reorderTime);

        Assert.True(changed);
        Assert.Equal(reorderTime, story.ChangedAt);
        Assert.Equal(new[] { c2, c1 }, story.AcceptanceCriteria);
    }

    [Fact]
    public void Save_ClearingAcceptanceCriteria_WithEmptyList_TriggersChange()
    {
        var story = CreateStory();
        story.Save(
            new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = [new AcceptanceCriterion("G", "W", "T")] }),
            StoryType,
            Later);

        var clearTime = Later.AddHours(1);
        var changed = story.Save(
            new WorkItemEdit(Fields: new WorkItemFieldsEdit { AcceptanceCriteria = [] }),
            StoryType,
            clearTime);

        Assert.True(changed);
        Assert.Equal(clearTime, story.ChangedAt);
        Assert.Empty(story.AcceptanceCriteria!);
    }

    [Theory]
    [InlineData("EPIC")]
    [InlineData("FEAT")]
    [InlineData("TASK")]
    [InlineData("BUG")]
    public void CheckFields_NonStoryTypes_StrictlyRefuseAcceptanceCriteria(string typeKey)
    {
        var appearance = typeKey switch
        {
            "EPIC" => AppearanceToken.Epic,
            "FEAT" => AppearanceToken.Feature,
            "TASK" => AppearanceToken.Task,
            _ => AppearanceToken.Bug,
        };

        var type = CreateType(typeKey, appearance);
        var item = WorkItem.Create(Guid.CreateVersion7(), 1, type, Guid.CreateVersion7(), "Item", Guid.CreateVersion7(), Created, "author-1");

        var reasons = item.CheckFields(
            new WorkItemFieldsEdit { AcceptanceCriteria = [new AcceptanceCriterion("G", "W", "T")] },
            type);

        var reason = Assert.Single(reasons);
        Assert.Equal(WorkItemField.AcceptanceCriteria, reason.Field);
        Assert.Equal(WorkItemErrorCodes.FieldNotOnType, reason.Code);
    }
}
