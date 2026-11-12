using Mustask.Domain.Iterations;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.Iterations;

/// <summary>The sprint's lifecycle and dates (mod-007 BR-1, BR-3, BR-4, BR-6, BR-7; US-1, US-2, US-5).</summary>
public sealed class SprintTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Start = new(2026, 11, 2);
    private static readonly DateOnly End = new(2026, 11, 13);

    private static SprintName Name(string value)
    {
        Assert.True(SprintName.TryCreate(value, out var name));

        return name;
    }

    private static Sprint Planned(SprintDates? dates = null, int number = 1) =>
        Sprint.Create(Guid.NewGuid(), Guid.NewGuid(), number, Name("Спринт 1"), dates ?? SprintDates.None, Now, "user");

    private static Sprint Active() => Started(new SprintDates(Start, End));

    private static Sprint Started(SprintDates dates)
    {
        var sprint = Planned(dates);
        sprint.Start();

        return sprint;
    }

    // US-1 · a new sprint is Planned and carries the number the caller took.
    [Fact]
    public void Create_IsPlanned_WithTheNumberAndTheCreator()
    {
        var sprint = Planned(number: 7);

        Assert.Equal(SprintState.Planned, sprint.State);
        Assert.Equal(7, sprint.Number);
        Assert.Equal("user", sprint.CreatedBy);
        Assert.Null(sprint.ClosedAt);
        Assert.Null(sprint.ClosedBy);
    }

    [Fact]
    public void Create_WithAReversedPair_IsAProgrammerError()
    {
        Assert.Throws<ArgumentException>(() => Planned(new SprintDates(End, Start)));
    }

    // US-2 · "Start": a Planned sprint with both dates becomes Active.
    [Fact]
    public void Start_APlannedSprintWithBothDates_BecomesActive()
    {
        var sprint = Started(new SprintDates(Start, End));

        Assert.Equal(SprintState.Active, sprint.State);
    }

    [Fact]
    public void Start_WithoutDates_IsRefused()
    {
        var error = Assert.Throws<ConflictException>(() => Planned().Start());

        Assert.Equal(SprintErrorCodes.StartDatesRequired, error.Code);
    }

    [Fact]
    public void Start_AnActiveOrClosedSprint_IsRefused()
    {
        var active = Active();
        var closed = Active();
        closed.Close(Now, "closer");

        Assert.Equal(SprintErrorCodes.NotPlanned, Assert.Throws<ConflictException>(active.Start).Code);
        Assert.Equal(SprintErrorCodes.NotPlanned, Assert.Throws<ConflictException>(closed.Start).Code);
    }

    // US-5 · "Close": an Active sprint becomes Closed with the time and the person.
    [Fact]
    public void Close_AnActiveSprint_RecordsTheTimeAndThePerson()
    {
        var sprint = Active();

        sprint.Close(Now, "closer");

        Assert.Equal(SprintState.Closed, sprint.State);
        Assert.Equal(Now, sprint.ClosedAt);
        Assert.Equal("closer", sprint.ClosedBy);
    }

    [Fact]
    public void Close_APlannedOrClosedSprint_IsRefused()
    {
        var closed = Active();
        closed.Close(Now, "closer");

        Assert.Equal(SprintErrorCodes.NotActive, Assert.Throws<ConflictException>(() => Planned().Close(Now, "x")).Code);
        Assert.Equal(SprintErrorCodes.NotActive, Assert.Throws<ConflictException>(() => closed.Close(Now, "x")).Code);
    }

    // US-1 · "Edit" by state (BR-4).
    [Fact]
    public void Update_APlannedSprint_TakesAnyAcceptablePair()
    {
        var sprint = Planned(new SprintDates(Start, End));

        sprint.Update(Name("  Релиз  "), SprintDates.None);

        Assert.Equal("Релиз", sprint.Name);
        Assert.Equal("РЕЛИЗ", sprint.NormalizedName);
        Assert.Null(sprint.StartDate);
        Assert.Null(sprint.EndDate);
    }

    [Fact]
    public void Update_AnActiveSprint_MustKeepBothDates()
    {
        var sprint = Active();

        var error = Assert.Throws<ConflictException>(() => sprint.Update(Name("Новое"), SprintDates.None));

        Assert.Equal(SprintErrorCodes.ActiveDatesRequired, error.Code);

        sprint.Update(Name("Новое"), new SprintDates(Start.AddDays(1), End.AddDays(1)));
        Assert.Equal(Start.AddDays(1), sprint.StartDate);
    }

    [Fact]
    public void Update_AClosedSprint_TakesTheNameAndNotTheDates()
    {
        var sprint = Active();
        sprint.Close(Now, "closer");

        var error = Assert.Throws<ConflictException>(() => sprint.Update(Name("Итог"), new SprintDates(Start, End.AddDays(1))));
        Assert.Equal(SprintErrorCodes.ClosedDatesLocked, error.Code);

        sprint.Update(Name("Итог"), new SprintDates(Start, End));
        Assert.Equal("Итог", sprint.Name);
        Assert.Equal(End, sprint.EndDate);
    }

    // BR-4 · both dates or neither, the finish not before the start.
    [Fact]
    public void Dates_NoneAtAll_AreAcceptable()
    {
        Assert.Null(SprintDates.None.Check());
    }

    [Fact]
    public void Dates_OneWithoutTheOther_AreUnpaired()
    {
        Assert.Equal(SprintErrorCodes.DatesUnpaired, new SprintDates(Start, null).Check());
        Assert.Equal(SprintErrorCodes.DatesUnpaired, new SprintDates(null, End).Check());
    }

    [Fact]
    public void Dates_AFinishBeforeTheStart_AreReversed()
    {
        Assert.Equal(SprintErrorCodes.DatesReversed, new SprintDates(End, Start).Check());
        Assert.Null(new SprintDates(Start, Start).Check());
        Assert.Null(new SprintDates(Start, End).Check());
    }

    // BR-7 · names: trimmed, 1..100, compared ignoring case.
    [Theory]
    [InlineData("Спринт", true)]
    [InlineData("  с  ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void Name_IsTrimmedAndNotEmpty(string? raw, bool valid)
    {
        Assert.Equal(valid, SprintName.TryCreate(raw, out _));
    }

    [Fact]
    public void Name_OfTheLimit_IsAcceptable_OneOver_IsNot()
    {
        Assert.True(SprintName.TryCreate(new string('я', SprintLimits.NameMaxLength), out _));
        Assert.False(SprintName.TryCreate(new string('я', SprintLimits.NameMaxLength + 1), out _));
        Assert.True(SprintName.TryCreate("  " + new string('я', SprintLimits.NameMaxLength) + "  ", out _));
    }

    [Fact]
    public void Name_ComparedIgnoringCaseAndSurroundingSpaces()
    {
        Assert.Equal(Name("Релиз").Normalized, Name("  рЕЛИЗ ").Normalized);
        Assert.NotEqual(Name("Релиз").Normalized, Name("Релиз 2").Normalized);
    }

    [Fact]
    public void Name_TheDefaultIsSprintAndTheNumber()
    {
        Assert.Equal("Спринт 12", SprintName.Default(12).Value);
    }
}
