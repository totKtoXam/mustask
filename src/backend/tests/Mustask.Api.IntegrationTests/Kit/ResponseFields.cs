using Mustask.Api.IntegrationTests.Features.Organizations;
using System.Text.Json;

namespace Mustask.Api.IntegrationTests.Kit;

/// <summary>
/// The response body has exactly these properties, no more and no fewer, in any order — the one assertion that replaces the
/// <c>PropertyNames</c> helper each endpoint test class copied (tech-035). A new field in the answer fails the test that does not
/// name it, which is how a contract change is noticed.
/// </summary>
public static class ResponseFields
{
    public static void AssertExactly(JsonElement body, params string[] names)
    {
        Assert.True(body.ValueKind == JsonValueKind.Object, $"The body is {body.ValueKind}, not an object.");

        var actual = body.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
        var expected = names.Order(StringComparer.Ordinal).ToArray();

        Assert.True(
            actual.SequenceEqual(expected, StringComparer.Ordinal),
            $"Response fields differ. Missing: [{string.Join(", ", expected.Except(actual))}]; unexpected: [{string.Join(", ", actual.Except(expected))}].");
    }

    public static void AssertExactly(Answer answer, params string[] names) => AssertExactly(answer.Body, names);
}
