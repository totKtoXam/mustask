using Mustask.Domain.Organizations;

namespace Mustask.Domain.Tests.Organizations;

public sealed class AppointmentReasonTests
{
    // feat-033 US-2 "The reason is required".
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \n")]
    public void MissingOrBlank_IsRequired(string? raw)
    {
        Assert.False(AppointmentReason.TryCreate(raw, out _, out var error));
        Assert.Equal(OrganizationErrorCodes.RescueReasonRequired, error!.Code);
    }

    [Fact]
    public void Text_IsTrimmed()
    {
        Assert.True(AppointmentReason.TryCreate("  Борис уволился \n", out var reason, out var error));
        Assert.Null(error);
        Assert.Equal("Борис уволился", reason.Value);
    }

    [Fact]
    public void FiveHundredCodePoints_AreValid_AndAnEmojiCountsAsOne()
    {
        Assert.True(AppointmentReason.TryCreate(new string('a', 500), out _, out _));
        Assert.True(AppointmentReason.TryCreate(string.Concat(Enumerable.Repeat("😀", 500)), out var reason, out _));
        Assert.Equal(1000, reason.Value.Length);
    }

    [Fact]
    public void FiveHundredAndOneCodePoints_AreTooLong_CountedAfterTrimming()
    {
        Assert.False(AppointmentReason.TryCreate(new string('a', 501), out _, out var error));
        Assert.Equal(OrganizationErrorCodes.RescueReasonTooLong, error!.Code);

        Assert.False(AppointmentReason.TryCreate(string.Concat(Enumerable.Repeat("😀", 501)), out _, out error));
        Assert.Equal(OrganizationErrorCodes.RescueReasonTooLong, error!.Code);

        Assert.True(AppointmentReason.TryCreate("  " + new string('a', 500) + "  ", out _, out _));
    }

    [Theory]
    [InlineData("a\0b")]
    [InlineData("a\uFFFEb")]
    [InlineData("a\uFDD0b")]
    public void NulAndNoncharacters_AreInvalid(string raw)
    {
        Assert.False(AppointmentReason.TryCreate(raw, out _, out var error));
        Assert.Equal(OrganizationErrorCodes.RescueReasonInvalid, error!.Code);
    }
}
