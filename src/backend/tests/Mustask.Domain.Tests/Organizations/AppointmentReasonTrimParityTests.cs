using Mustask.Domain.Organizations;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>
/// feat-033 BR-RS-5 "the dialog and the server count the same way" (backend QA): the panel trims «Причина» with
/// <c>String.prototype.trim</c>, which removes every Unicode space separator, the line separators and U+FEFF. A reason made
/// only of those is «Укажите причину» on the server as well, never a stored reason nobody can see. The characters are given
/// as code points so the source holds no invisible text.
/// </summary>
public sealed class AppointmentReasonTrimParityTests
{
    private static string Text(params int[] codePoints) => string.Concat(codePoints.Select(code => (char)code));

    [Theory]
    [InlineData(0x00A0)]
    [InlineData(0x3000)]
    [InlineData(0x2003, 0x2028)]
    [InlineData(0xFEFF)]
    [InlineData(0x20, 0xFEFF, 0x20)]
    public void OnlyCharactersThePanelTrims_IsRequired(params int[] codePoints)
    {
        Assert.False(AppointmentReason.TryCreate(Text(codePoints), out _, out var error));
        Assert.Equal(OrganizationErrorCodes.RescueReasonRequired, error!.Code);
    }

    [Fact]
    public void ALeadingByteOrderMark_IsNotKept()
    {
        Assert.True(AppointmentReason.TryCreate(Text(0xFEFF) + "Борис уволился", out var reason, out _));
        Assert.Equal("Борис уволился", reason.Value);
    }
}
