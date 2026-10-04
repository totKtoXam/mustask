using Mustask.Domain.Process;

namespace Mustask.Domain.Tests.Process;

/// <summary>
/// plan-backend.md § 3: "Seed checks these invariants, so any edit to the
/// blueprint fails at once". Each test breaks the shipped blueprint in exactly
/// one way and expects the check to refuse it.
/// </summary>
public sealed class ProcessBlueprintTests
{
    private static readonly ProcessBlueprint Shipped = SeededProcess.Blueprint;

    private static ProcessBlueprint WithTypes(Func<List<TypeBlueprint>, List<TypeBlueprint>> change) =>
        Shipped with { Types = change([.. Shipped.Types]) };

    [Fact]
    public void EnsureValid_TypeWithoutStates_Throws()
    {
        var broken = WithTypes(types => [.. types.Take(4), types[4] with { States = [] }]);

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }

    [Fact]
    public void EnsureValid_RemovedStateNotLast_Throws()
    {
        var task = Shipped.Types[^1];
        var broken = WithTypes(types => [.. types.Take(4), task with { States = [.. task.States.Reverse()] }]);

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }

    [Fact]
    public void EnsureValid_TwoRemovedStates_Throws()
    {
        var task = Shipped.Types[^1];
        var broken = WithTypes(types => [.. types.Take(4), task with { States = [.. task.States, new StateBlueprint("Архив", StateCategory.Removed)] }]);

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }

    [Fact]
    public void EnsureValid_DuplicateStateNameInOneType_Throws()
    {
        var task = Shipped.Types[^1];
        var broken = WithTypes(types => [.. types.Take(4), task with { States = [task.States[0], task.States[0], .. task.States.Skip(1)] }]);

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }

    [Fact]
    public void EnsureValid_DuplicateKey_Throws()
    {
        var broken = WithTypes(types => [.. types.Take(4), types[4] with { Key = "BUG" }]);

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }

    [Fact]
    public void EnsureValid_DuplicateName_Throws()
    {
        var broken = WithTypes(types => [.. types.Take(4), types[4] with { Name = "Баг" }]);

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }

    [Fact]
    public void EnsureValid_TwoTypesShareAToken_Throws()
    {
        var broken = WithTypes(types => [.. types.Take(4), types[4] with { Appearance = AppearanceToken.Bug }]);

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }

    [Fact]
    public void EnsureValid_TypeOnALevelOfAnotherProcess_Throws()
    {
        var foreign = new LevelBlueprint("Чужой", 9, BacklogLevelKind.Task);
        var broken = WithTypes(types => [.. types.Take(4), types[4] with { Level = foreign }]);

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }

    [Fact]
    public void EnsureValid_DuplicateLevelRank_Throws()
    {
        var broken = Shipped with { Levels = [.. Shipped.Levels.Take(3), Shipped.Levels[3] with { Rank = 2 }] };

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }

    [Fact]
    public void EnsureValid_DuplicateLevelName_Throws()
    {
        var broken = Shipped with { Levels = [.. Shipped.Levels.Take(3), Shipped.Levels[3] with { Name = "Эпики" }] };

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }

    [Fact]
    public void EnsureValid_NameLongerThanTheColumn_Throws()
    {
        var tooLong = new string('я', WorkItemType.NameMaxLength + 1);
        var broken = WithTypes(types => [.. types.Take(4), types[4] with { Name = tooLong }]);

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }

    [Fact]
    public void EnsureValid_KeyLongerThanTheColumn_Throws()
    {
        var tooLong = new string('A', WorkItemType.KeyMaxLength + 1);
        var broken = WithTypes(types => [.. types.Take(4), types[4] with { Key = tooLong }]);

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }

    [Fact]
    public void EnsureValid_EmptyName_Throws()
    {
        var broken = WithTypes(types => [.. types.Take(4), types[4] with { Name = " " }]);

        Assert.Throws<InvalidOperationException>(broken.EnsureValid);
    }
}
