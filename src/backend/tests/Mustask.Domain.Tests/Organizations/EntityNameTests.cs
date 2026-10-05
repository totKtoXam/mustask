using Mustask.Domain.Organizations;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>BR-M1-1: one name rule for organizations, projects, teams and areas.</summary>
public sealed class EntityNameTests
{
    // US-1 · "The name is trimmed and its length is checked": surrounding spaces are not part of the name.
    [Fact]
    public void TryCreate_PaddedName_StoresTrimmed()
    {
        Assert.True(EntityName.TryCreate("  Пример организации  ", out var name));

        Assert.Equal("Пример организации", name.Value);
        Assert.Equal("ПРИМЕР ОРГАНИЗАЦИИ", name.Normalized);
    }

    // US-1 · "The name is trimmed and its length is checked": 1 and 100 characters after trimming are the bounds.
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void TryCreate_LengthOnTheBound_Succeeds(int length)
    {
        Assert.True(EntityName.TryCreate(" " + new string('я', length) + " ", out var name));
        Assert.Equal(length, name.Value.Length);
    }

    // US-1 · "The name is trimmed and its length is checked": empty, spaces only and 101 characters are refused.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void TryCreate_EmptyOrSpacesOnly_Fails(string? raw)
    {
        Assert.False(EntityName.TryCreate(raw, out _));
    }

    [Fact]
    public void TryCreate_101CharactersAfterTrimming_Fails()
    {
        Assert.False(EntityName.TryCreate(new string('я', 101), out _));
    }

    // US-1 · "unique instance-wide ... ignoring case and spaces": one comparison key for every spelling.
    [Theory]
    [InlineData("Пример организации", "пример организации ")]
    [InlineData("Ёлка", "ёЛКА")]
    [InlineData("Alpha Team", " ALPHA team")]
    public void Normalized_IgnoresLetterCaseAndSurroundingSpaces(string first, string second)
    {
        Assert.True(EntityName.TryCreate(first, out var a));
        Assert.True(EntityName.TryCreate(second, out var b));

        Assert.Equal(a.Normalized, b.Normalized);
        Assert.Equal(a.Normalized, EntityName.Normalize(second));
    }

    // plan-backend § 3: the key is as wide as the name, so the 100-wide column always holds it.
    [Fact]
    public void Normalized_HasTheLengthOfTheName()
    {
        var raw = new string('ß', 50) + new string('ﬀ', 50);

        Assert.True(EntityName.TryCreate(raw, out var name));
        Assert.Equal(name.Value.Length, name.Normalized.Length);
    }

    [Fact]
    public void LengthReason_IsTheNameLengthCode()
    {
        Assert.Equal(OrganizationErrorCodes.NameLength, EntityName.LengthReason.Code);
    }
}
