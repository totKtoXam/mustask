using Mustask.Domain.Boards;
using Mustask.Domain.Process;

namespace Mustask.Domain.Tests.Boards;

/// <summary>The kinds a card's checklist holds (feat-032 BR-C-1, BR-C-2).</summary>
public sealed class CardChecklistTests
{
    // feat-032 US-1 · "Only task, bug and test-case children are listed" (BR-C-1): by appearance, in the counters' order.
    [Fact]
    public void Kinds_AreTaskBugAndTestCase_InTheCountersOrder()
    {
        Assert.Equal([AppearanceToken.Task, AppearanceToken.Bug, AppearanceToken.Testcase], CardChecklist.Kinds);
    }

    [Theory]
    [InlineData(AppearanceToken.Task, true)]
    [InlineData(AppearanceToken.Bug, true)]
    [InlineData(AppearanceToken.Testcase, true)]
    [InlineData(AppearanceToken.Story, false)]
    [InlineData(AppearanceToken.Feature, false)]
    [InlineData(AppearanceToken.Epic, false)]
    public void Holds_OnlyTheThreeKinds(AppearanceToken kind, bool held)
    {
        Assert.Equal(held, CardChecklist.Holds(kind));
    }
}
