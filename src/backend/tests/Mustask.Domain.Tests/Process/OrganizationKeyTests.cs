using Mustask.Domain.Process;

namespace Mustask.Domain.Tests.Process;

/// <summary>
/// status.md D-10 / D-9: "malformed" has one definition, shared by the seed and
/// the read. US-1 "A malformed identifier is refused", US-2 "An identifier with
/// no process is not found".
/// </summary>
public sealed class OrganizationKeyTests
{
    [Fact]
    public void IsWellFormed_AnyNonEmptyGuid_ReturnsTrue()
    {
        Assert.True(OrganizationKey.IsWellFormed(Guid.Parse("d0000000-0000-4000-8000-000000000001")));
    }

    [Fact]
    public void IsWellFormed_EmptyGuid_ReturnsFalse()
    {
        Assert.False(OrganizationKey.IsWellFormed(Guid.Empty));
    }

    [Theory]
    [InlineData("d0000000-0000-4000-8000-000000000001")]
    [InlineData("D0000000-0000-4000-8000-00000000000A")]
    [InlineData("0199a3f2-5b7c-7d10-9a4e-3c2b1a0f9e8d")]
    public void TryParse_CanonicalForm_ReturnsTheGuid(string value)
    {
        var ok = OrganizationKey.TryParse(value, out var id);

        Assert.True(ok);
        Assert.Equal(Guid.Parse(value), id);
    }

    // The values of the US-2 theory: not a UUID, all zeros, 32 hex digits, braced, one character short, a trailing character.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("d00000000000400080000000000000001")]
    [InlineData("{d0000000-0000-4000-8000-000000000001}")]
    [InlineData("(d0000000-0000-4000-8000-000000000001)")]
    [InlineData("d0000000-0000-4000-8000-00000000001")]
    [InlineData("d0000000-0000-4000-8000-0000000000011")]
    [InlineData("d0000000-0000-4000-8000-000000000001 ")]
    [InlineData(" d0000000-0000-4000-8000-000000000001")]
    [InlineData("d0000000-0000-4000-8000-00000000000g")]
    public void TryParse_AnythingElse_ReturnsFalseAndAnEmptyGuid(string? value)
    {
        var ok = OrganizationKey.TryParse(value, out var id);

        Assert.False(ok);
        Assert.Equal(Guid.Empty, id);
    }
}
