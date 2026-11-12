using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>
/// feat-018 US-4: the format marker, the three limits and the (value, format) equality, as the domain decides them
/// without a parser (the reduction itself is Infrastructure's).
/// </summary>
public sealed class WorkItemDescriptionTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddHours(1);

    private static readonly WorkItemTypeStates Task = new(
        Guid.CreateVersion7(),
        "TASK",
        AppearanceToken.Task,
        [
            new WorkItemTypeState(Guid.CreateVersion7(), 1, StateCategory.Proposed),
            new WorkItemTypeState(Guid.CreateVersion7(), 2, StateCategory.Completed),
        ]);

    private static WorkItem AnItem() =>
        WorkItem.Create(Guid.CreateVersion7(), 1, Task, Guid.CreateVersion7(), "Заголовок", Guid.CreateVersion7(), Created, "sub");

    private static WorkItem AnItemWith(string description, DescriptionFormat format)
    {
        var item = AnItem();
        item.Save(new WorkItemEdit(Description: description, DescriptionFormat: format), Task, Created);

        return item;
    }

    // US-4 · "A never-written or cleared description is plain and empty": Create is ("", plain).
    [Fact]
    public void Create_DescriptionIsEmptyAndPlain()
    {
        var item = AnItem();

        Assert.Equal(string.Empty, item.Description);
        Assert.Equal(DescriptionFormat.Plain, item.DescriptionFormat);
    }

    // US-4 · "An unknown format is refused": exact and case-sensitive, no trimming; null is "not declared".
    [Theory]
    [InlineData("plain", DescriptionFormat.Plain)]
    [InlineData("html", DescriptionFormat.Html)]
    public void ValidateFormat_KnownKeys_Accepted(string raw, DescriptionFormat expected)
    {
        Assert.Null(WorkItemText.ValidateFormat(raw, out var format));
        Assert.Equal(expected, format);
    }

    [Theory]
    [InlineData("markdown")]
    [InlineData("HTML")]
    [InlineData("Plain")]
    [InlineData("")]
    [InlineData(" html")]
    [InlineData("html ")]
    public void ValidateFormat_AnythingElse_IsFormatUnknown(string raw)
    {
        Assert.Equal(WorkItemErrorCodes.DescriptionFormatUnknown, WorkItemText.ValidateFormat(raw, out var format));
        Assert.Null(format);
    }

    [Fact]
    public void ValidateFormat_NotDeclared_NoReasonNoFormat()
    {
        Assert.Null(WorkItemText.ValidateFormat(null, out var format));
        Assert.Null(format);
    }

    // US-4 · plain keeps BR-M3-19: 100 000 UTF-16 units inclusive.
    [Fact]
    public void ValidatePlain_100000Accepted_100001Refused()
    {
        Assert.Null(WorkItemText.ValidatePlain(new string('a', WorkItemLimits.DescriptionMaxLength)));
        Assert.Equal(WorkItemErrorCodes.DescriptionTooLong, WorkItemText.ValidatePlain(new string('a', WorkItemLimits.DescriptionMaxLength + 1)));
    }

    // US-4 · "An oversized request is refused before parsing": 2 000 000 units inclusive.
    [Fact]
    public void CheckRequestSize_2000000Accepted_2000001Refused_AsMarkupTooLarge()
    {
        Assert.Null(WorkItemText.CheckRequestSize(new string('a', WorkItemLimits.DescriptionRequestMaxLength)));
        Assert.Equal(
            WorkItemErrorCodes.DescriptionMarkupTooLarge,
            WorkItemText.CheckRequestSize(new string('a', WorkItemLimits.DescriptionRequestMaxLength + 1)));
    }

    // US-4 · "Exactly the limit is accepted", "The visible limit is enforced", "The markup ceiling has its own reason",
    // "Over both limits, the markup reason wins".
    [Theory]
    [InlineData(1_000, 100_000, null)]
    [InlineData(1_000, 100_001, WorkItemErrorCodes.DescriptionTooLong)]
    [InlineData(1_000_000, 50_000, null)]
    [InlineData(1_000_001, 50_000, WorkItemErrorCodes.DescriptionMarkupTooLarge)]
    [InlineData(1_000_001, 100_001, WorkItemErrorCodes.DescriptionMarkupTooLarge)]
    public void ValidateReduced_MarkupFirstThenVisible(int markup, int visible, string? expected)
    {
        Assert.Equal(expected, WorkItemText.ValidateReduced(markup, visible));
    }

    // US-4 · "Limits come before the empty rule"'s domain half: no content is "" plain, content is the html.
    [Fact]
    public void Describe_NoContent_IsEmptyPlain_ContentIsHtml()
    {
        Assert.Equal(WorkItemDescription.Empty, WorkItemText.Describe("<p> </p>", hasContent: false));
        Assert.Equal(("<p>a</p>", DescriptionFormat.Html), Pair(WorkItemText.Describe("<p>a</p>", hasContent: true)));
    }

    [Fact]
    public void Of_EmptyValue_IsPlainWhateverTheFormat()
    {
        Assert.Equal(WorkItemDescription.Empty, WorkItemDescription.Of(string.Empty, DescriptionFormat.Html));
    }

    [Fact]
    public void CodePoints_CountsSurrogatePairsOnce_AndALoneSurrogateOnce()
    {
        Assert.Equal(3, WorkItemText.CodePoints("a😀b"));
        Assert.Equal(3, WorkItemText.CodePoints("a\uD83Db"));
    }

    // US-4 · "Equality is of value and format together", the four rows of api-contract § 1.2 over ("<p>a</p>", html).
    [Fact]
    public void Save_SameHtmlPair_ChangesNothing_ChangedAtStays()
    {
        var item = AnItemWith("<p>a</p>", DescriptionFormat.Html);

        Assert.False(item.Save(new WorkItemEdit(Description: "<p>a</p>", DescriptionFormat: DescriptionFormat.Html), Task, Later));
        Assert.Equal(Created, item.ChangedAt);
    }

    [Fact]
    public void Save_DifferentHtmlValue_IsAChange()
    {
        var item = AnItemWith("<p>a</p>", DescriptionFormat.Html);

        Assert.True(item.Save(new WorkItemEdit(Description: "<p><strong>a</strong></p>", DescriptionFormat: DescriptionFormat.Html), Task, Later));
        Assert.Equal(Later, item.ChangedAt);
    }

    // US-4 · "A plain write over an html description is honoured": same string, no declaration, is a change.
    [Fact]
    public void Save_SameValueAsPlain_OverHtml_IsAChange_StoredPlainAsSent()
    {
        var item = AnItemWith("<p>a</p>", DescriptionFormat.Html);

        Assert.True(item.Save(new WorkItemEdit(Description: "<p>a</p>"), Task, Later));
        Assert.Equal(("<p>a</p>", DescriptionFormat.Plain, Later), (item.Description, item.DescriptionFormat, item.ChangedAt));
    }

    [Fact]
    public void Save_EmptyPlain_OverHtml_ClearsIt()
    {
        var item = AnItemWith("<p>a</p>", DescriptionFormat.Html);

        Assert.True(item.Save(new WorkItemEdit(Description: string.Empty), Task, Later));
        Assert.Equal((string.Empty, DescriptionFormat.Plain), (item.Description, item.DescriptionFormat));
    }

    // US-4 · "a cleared description is plain and empty": the handler turns an html "" into ("", plain), and that over
    // ("", plain) is not a change; an html pair with an empty value never reaches the entity.
    [Fact]
    public void Save_EmptyPlainOverEmptyPlain_IsNotAChange_AndEmptyHtmlIsADefect()
    {
        var item = AnItem();

        Assert.False(item.Save(new WorkItemEdit(Description: WorkItemText.Describe("<p></p>", hasContent: false).Value), Task, Later));
        Assert.Equal(Created, item.ChangedAt);
        Assert.Throws<ArgumentException>(() =>
            item.Save(new WorkItemEdit(Description: string.Empty, DescriptionFormat: DescriptionFormat.Html), Task, Later));
    }

    // US-3 server half: a save that sends no description leaves the pair as it was.
    [Fact]
    public void Save_AnotherFieldOnly_LeavesDescriptionAndFormat()
    {
        var item = AnItemWith("<p>a</p>", DescriptionFormat.Html);

        Assert.True(item.Save(new WorkItemEdit(Title: "Другой"), Task, Later));
        Assert.Equal(("<p>a</p>", DescriptionFormat.Html), (item.Description, item.DescriptionFormat));
    }

    // The entity guards its stored invariants (a caller defect, the handler validates first).
    [Fact]
    public void Save_InconsistentEdits_AreDefects()
    {
        var item = AnItem();

        Assert.Throws<ArgumentException>(() =>
            item.Save(new WorkItemEdit(Description: new string('a', WorkItemLimits.DescriptionMaxLength + 1)), Task, Later));
        Assert.Throws<ArgumentException>(() =>
            item.Save(new WorkItemEdit(Description: new string('a', WorkItemLimits.DescriptionMarkupMaxLength + 1), DescriptionFormat: DescriptionFormat.Html), Task, Later));
        Assert.Throws<ArgumentException>(() =>
            item.Save(new WorkItemEdit(DescriptionFormat: DescriptionFormat.Html), Task, Later));
        Assert.Equal((string.Empty, DescriptionFormat.Plain), (item.Description, item.DescriptionFormat));
    }

    // US-4 · "Exactly the limit is accepted" for the entity: 100 000 emoji is 200 000 units but 100 000 code points of
    // visible text; markup up to 1 000 000 code points fits the entity.
    [Fact]
    public void Save_HtmlAtTheMarkupCeiling_IsAccepted()
    {
        var item = AnItem();
        var markup = new string('a', WorkItemLimits.DescriptionMarkupMaxLength);

        Assert.True(item.Save(new WorkItemEdit(Description: markup, DescriptionFormat: DescriptionFormat.Html), Task, Later));
        Assert.Equal(DescriptionFormat.Html, item.DescriptionFormat);
    }

    [Theory]
    [InlineData("plain", true)]
    [InlineData("html", true)]
    [InlineData("HTML", false)]
    public void Key_RoundTripsWithTryParse(string key, bool known)
    {
        Assert.Equal(known, DescriptionFormats.TryParse(key, out var format));

        if (known)
        {
            Assert.Equal(key, DescriptionFormats.Key(format));
        }
    }

    private static (string Value, DescriptionFormat Format) Pair(WorkItemDescription description) => (description.Value, description.Format);
}
