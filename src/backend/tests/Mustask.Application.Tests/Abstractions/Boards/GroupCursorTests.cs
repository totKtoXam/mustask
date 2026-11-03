using Mustask.Application.Abstractions.Boards;

namespace Mustask.Application.Tests.Abstractions.Boards;

/// <summary>The opaque cursor of a group read (ADR-0036): it round-trips a position, and refuses a foreign or a broken one.</summary>
public sealed class GroupCursorTests
{
    private const string Fingerprint = "Backlogs|org|stories|Team|d=|p=|t=|s=|a=|q=|c=|g=none";

    [Fact]
    public void ChangedPosition_RoundTrips()
    {
        var position = new ChangedPosition(639_000_000_000_000_000, 42, "ALP");

        var text = GroupCursor.Encode(position, Fingerprint);

        Assert.True(GroupCursor.TryDecode(text, Fingerprint, rank: false, out var decoded));
        Assert.Equal(position, decoded);
    }

    [Fact]
    public void RankPosition_RoundTrips()
    {
        var position = new RankPosition(1, 0, 639_000_000_000_000_000, 7);

        var text = GroupCursor.Encode(position, Fingerprint);

        Assert.True(GroupCursor.TryDecode(text, Fingerprint, rank: true, out var decoded));
        Assert.Equal(position, decoded);
    }

    [Fact]
    public void TheCursor_IsUrlSafe()
    {
        var text = GroupCursor.Encode(new ChangedPosition(1, 1, "A/B+C"), Fingerprint);

        Assert.DoesNotContain('+', text);
        Assert.DoesNotContain('/', text);
        Assert.DoesNotContain('=', text);
    }

    [Fact]
    public void ACursorOfAnotherRequest_IsRefused()
    {
        var text = GroupCursor.Encode(new ChangedPosition(1, 1, "ALP"), Fingerprint);

        Assert.False(GroupCursor.TryDecode(text, Fingerprint + "x", rank: false, out _));
    }

    [Fact]
    public void ARankCursor_IsNotAChangeCursor_AndTheOtherWayRound()
    {
        var change = GroupCursor.Encode(new ChangedPosition(1, 1, "ALP"), Fingerprint);
        var rank = GroupCursor.Encode(new RankPosition(0, 1, 1, 1), Fingerprint);

        Assert.False(GroupCursor.TryDecode(change, Fingerprint, rank: true, out _));
        Assert.False(GroupCursor.TryDecode(rank, Fingerprint, rank: false, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("!!!")]
    [InlineData("e30")]
    [InlineData("bm90LWpzb24")]
    public void ABrokenCursor_IsRefused_NeverThrown(string text) =>
        Assert.False(GroupCursor.TryDecode(text, Fingerprint, rank: false, out _));
}
