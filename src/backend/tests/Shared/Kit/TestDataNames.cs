using Mustask.Domain.Organizations;

namespace Mustask.Testing;

internal static partial class TestData
{
    /// <summary>A valid <see cref="EntityName"/>; a test passing an invalid one is itself defective, so it throws.</summary>
    public static EntityName Name(string value) =>
        EntityName.TryCreate(value, out var name) ? name : throw new ArgumentException($"«{value}» is not a valid name.", nameof(value));
}
