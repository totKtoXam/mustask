using Mustask.AppHost.Provisioning;

namespace Mustask.Contracts.Tests;

/// <summary>
/// mod-014 D-10 (b) (ADR-0025 § 7): the stand reset's own rules — it never
/// drops a PostgreSQL system database, quotes the one it drops, and its flags
/// never reach Aspire. The reset itself (Docker, PostgreSQL) is proven on the
/// live stand at QA, with a second database on the same server left intact.
/// </summary>
public sealed class StandResetRulesTests
{
    [Theory]
    [InlineData("postgres")]
    [InlineData("template0")]
    [InlineData("TEMPLATE1")]
    [InlineData(" postgres ")]
    [InlineData("")]
    public void SystemDatabases_AreNeverReset(string database)
    {
        Assert.False(StandResetRules.MayReset(database));
    }

    [Theory]
    [InlineData("mustask")]
    [InlineData("mustask_mod014")]
    public void TheStandsDatabase_MayBeReset(string database)
    {
        Assert.True(StandResetRules.MayReset(database));
    }

    [Theory]
    [InlineData("mustask", "\"mustask\"")]
    [InlineData("odd\"name", "\"odd\"\"name\"")]
    [InlineData("Mixed Case", "\"Mixed Case\"")]
    public void TheDroppedDatabase_IsQuotedAsAnIdentifier(string database, string quoted)
    {
        Assert.Equal(quoted, StandResetRules.QuoteIdentifier(database));
    }

    [Fact]
    public void TheResetsFlags_AreTakenOutOfTheAppHostsArguments()
    {
        var parsed = StandResetRules.Parse(["--stand-reset", "--yes", "--launch-profile", "https"]);

        Assert.True(parsed.Reset);
        Assert.True(parsed.Yes);
        Assert.Equal(["--launch-profile", "https"], parsed.AppHostArgs);
    }

    [Fact]
    public void AnOrdinaryStart_IsNotAReset()
    {
        var parsed = StandResetRules.Parse([]);

        Assert.False(parsed.Reset);
        Assert.Empty(parsed.AppHostArgs);
    }

    [Fact]
    public void YesWithoutReset_IsRefused()
    {
        Assert.Throws<InvalidOperationException>(() => StandResetRules.Parse(["--yes"]));
    }
}
