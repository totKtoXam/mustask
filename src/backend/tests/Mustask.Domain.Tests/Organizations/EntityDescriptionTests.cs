using Mustask.Domain.Organizations;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>feat-028 BR-OL-3: one description rule for projects and teams — kept as typed, empty is none, at most 2 000.</summary>
public sealed class EntityDescriptionTests
{
    // US-1 · "rename and description": the text is kept exactly as typed, spaces and line breaks included.
    [Fact]
    public void TryCreate_Text_IsKeptExactlyAsSent()
    {
        const string typed = "  Платформа\r\n  и инструменты  ";

        Assert.True(EntityDescription.TryCreate(typed, out var description));

        Assert.Equal(typed, description!.Value.Value);
    }

    // US-1 · "rename and description": the empty string, null and an absent description are all "no description".
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryCreate_NullOrEmpty_IsNoDescription(string? raw)
    {
        Assert.True(EntityDescription.TryCreate(raw, out var description));

        Assert.Null(description);
    }

    // A blank description is text, not "none": only the empty string is.
    [Fact]
    public void TryCreate_SpacesOnly_IsKept()
    {
        Assert.True(EntityDescription.TryCreate("   ", out var description));

        Assert.Equal("   ", description!.Value.Value);
    }

    // US-1 · "a choice that cannot be made": 2 000 characters are accepted, 2 001 are refused (UTF-16 code units).
    [Theory]
    [InlineData(1, true)]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void TryCreate_LengthOnTheBound(int length, bool accepted)
    {
        Assert.Equal(accepted, EntityDescription.TryCreate(new string('я', length), out var description));
        Assert.Equal(accepted, description is not null);
    }

    [Fact]
    public void LengthReason_IsTheRegisteredFieldCode()
    {
        Assert.Equal(OrganizationErrorCodes.DescriptionLength, EntityDescription.LengthReason.Code);
    }
}
