using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>The rules of Заголовок (BR-M3-3) and Описание (BR-M3-19), without the database or the endpoint.</summary>
public sealed class WorkItemTextTests
{
    // US-1 · "A work item is created with only a title": «  Настроить сборку  » is stored trimmed.
    [Fact]
    public void ValidateTitle_SurroundingSpaces_AreTrimmed()
    {
        Assert.Null(WorkItemText.ValidateTitle("  Настроить сборку  ", out var title));
        Assert.Equal("Настроить сборку", title);
    }

    // US-1 · "An invalid title is refused" (empty, three spaces) and US-3 · "An empty title".
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \u00A0")]
    public void ValidateTitle_EmptyOrWhiteSpace_IsTitleRequired(string? raw)
    {
        Assert.Equal(WorkItemErrorCodes.TitleRequired, WorkItemText.ValidateTitle(raw, out _));
    }

    // US-1 · "A title of exactly 255 characters is accepted" and "256 characters after trimming" is refused.
    [Fact]
    public void ValidateTitle_255Accepted_256AfterTrimmingRefused()
    {
        var exactly = new string('я', 255);

        Assert.Null(WorkItemText.ValidateTitle($"  {exactly}  ", out var title));
        Assert.Equal(exactly, title);
        Assert.Equal(WorkItemErrorCodes.TitleTooLong, WorkItemText.ValidateTitle($" {exactly}я ", out _));
    }

    // US-1 · «Сборка» + a line break + «падает» is refused, whatever the line break; a break that trimming removes is not
    // in the title.
    [Theory]
    [InlineData("Сборка\nпадает")]
    [InlineData("Сборка\r\nпадает")]
    [InlineData("Сборка\rпадает")]
    [InlineData("Сборка\u2028падает")]
    [InlineData("Сборка\u2029падает")]
    [InlineData("Сборка\u0085падает")]
    [InlineData("Сборка\u000Bпадает")]
    public void ValidateTitle_LineBreakInside_IsTitleMultiline(string raw)
    {
        Assert.Equal(WorkItemErrorCodes.TitleMultiline, WorkItemText.ValidateTitle(raw, out _));
    }

    [Fact]
    public void ValidateTitle_TrailingLineBreak_IsTrimmedAway()
    {
        Assert.Null(WorkItemText.ValidateTitle("Сборка падает\n", out var title));
        Assert.Equal("Сборка падает", title);
    }

    // One reason per field, the first that applies: a long title with a line break is too long.
    [Fact]
    public void ValidateTitle_TooLongAndMultiline_IsTitleTooLong()
    {
        Assert.Equal(WorkItemErrorCodes.TitleTooLong, WorkItemText.ValidateTitle(new string('a', 200) + "\n" + new string('b', 200), out _));
    }

    // US-3 · "a description of 100 001 characters" is refused; 100 000 is accepted; markup and line breaks are kept as
    // they are (US-3 "The description is plain text").
    [Fact]
    public void ValidateDescription_100000Accepted_100001Refused_TextKeptAsIs()
    {
        Assert.Null(WorkItemText.ValidateDescription(new string('ж', 100_000)));
        Assert.Null(WorkItemText.ValidateDescription(string.Empty));
        Assert.Null(WorkItemText.ValidateDescription("<b>важно</b>\nшаг 2"));
        Assert.Equal(WorkItemErrorCodes.DescriptionTooLong, WorkItemText.ValidateDescription(new string('ж', 100_001)));
    }
}
