using Mustask.Domain.Organizations;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>US-3 · "The key is required and well formed" (BR-M1-1, design § 4.4).</summary>
public sealed class ProjectKeyTests
{
    [Theory]
    [InlineData("PRM")]
    [InlineData("P1")]
    [InlineData("AB")]
    [InlineData("ABCDEFGHIJ")]
    [InlineData("A123456789")]
    public void Validate_WellFormedKey_ReturnsNull(string key)
    {
        Assert.Null(ProjectKey.Validate(key));
        Assert.True(ProjectKey.IsWellFormed(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_MissingKey_IsKeyRequired(string? key)
    {
        Assert.Equal(OrganizationErrorCodes.ProjectKeyRequired, ProjectKey.Validate(key));
    }

    // The story's values, plus a key that is never trimmed or upper-cased, and a trailing line feed.
    [Theory]
    [InlineData("prm")]
    [InlineData("1PRM")]
    [InlineData("P")]
    [InlineData("PROJECTKEY01")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("РМК")]
    [InlineData(" PRM")]
    [InlineData("PRM ")]
    [InlineData("PR-M")]
    [InlineData("PRM\n")]
    [InlineData("   ")]
    public void Validate_MalformedKey_IsKeyInvalid(string key)
    {
        Assert.Equal(OrganizationErrorCodes.ProjectKeyInvalid, ProjectKey.Validate(key));
        Assert.False(ProjectKey.IsWellFormed(key));
    }
}
