using System.Text;
using System.Text.Json;
using Mustask.Api.Features.UserDirectory;
using Mustask.Application.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mustask.Contracts.Tests;

/// <summary>
/// mod-014 api-contract.md B.2: MUSTASK's verifier accepts exactly what the
/// provider signs — both read the same vectors, kept with the provider's
/// tests (<c>push-signature-vectors.json</c>; the provider's half is
/// <c>PushSignerTest</c>). A change to either side's canonical string fails
/// one of the two.
/// </summary>
[Trait("Category", "KeycloakProvider")]
public sealed class PushSignatureVectorsTests
{
    private static readonly string VectorsPath = Path.Combine(
        RepositoryLayout.Root.FullName,
        "deploy", "keycloak", "providers", "user-projection", "src", "test", "resources", "push-signature-vectors.json");

    public static TheoryData<string> VectorNames()
    {
        var names = new TheoryData<string>();

        foreach (var vector in Vectors())
        {
            names.Add(vector.GetProperty("name").GetString()!);
        }

        return names;
    }

    private static IEnumerable<JsonElement> Vectors()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorsPath));

        return document.RootElement.GetProperty("vectors").EnumerateArray().Select(vector => vector.Clone()).ToList();
    }

    private static JsonElement Vector(string name) =>
        Vectors().Single(vector => vector.GetProperty("name").GetString() == name);

    [Theory]
    [MemberData(nameof(VectorNames))]
    public void SharedVector_CanonicalStringAndSignature_MatchTheProviders(string name)
    {
        var vector = Vector(name);
        var body = Encoding.UTF8.GetBytes(vector.GetProperty("body").GetString()!);

        var canonical = PushSignatureVerifier.CanonicalString(
            vector.GetProperty("timestamp").GetString()!,
            vector.GetProperty("method").GetString()!,
            vector.GetProperty("target").GetString()!,
            body);

        Assert.Equal(vector.GetProperty("canonical").GetString(), canonical);
        Assert.Equal(vector.GetProperty("signature").GetString(), PushSignatureVerifier.Sign(vector.GetProperty("secret").GetString()!, canonical));
    }

    [Theory]
    [MemberData(nameof(VectorNames))]
    public void SharedVector_IsAcceptedByTheVerifier_AtItsOwnTimestamp(string name)
    {
        var vector = Vector(name);
        var timestamp = vector.GetProperty("timestamp").GetString()!;
        var verifier = new PushSignatureVerifier(
            new PushChannelOptions { Secret = vector.GetProperty("secret").GetString()! },
            new FixedClock(DateTimeOffset.FromUnixTimeSeconds(long.Parse(timestamp, System.Globalization.CultureInfo.InvariantCulture))),
            NullLogger<PushSignatureVerifier>.Instance);

        var refusal = verifier.Check(new PushRequestParts(
            vector.GetProperty("method").GetString()!,
            vector.GetProperty("target").GetString()!,
            "/api/user-directory/feed/" + vector.GetProperty("target").GetString(),
            timestamp,
            vector.GetProperty("signature").GetString(),
            HasBearer: false,
            Encoding.UTF8.GetBytes(vector.GetProperty("body").GetString()!)));

        Assert.Null(refusal);
    }

    [Fact]
    public void TheContractsWorkedExample_IsTheFirstVector()
    {
        var first = Vectors().First();

        Assert.Equal("v1=d4f1bd083b09951e40e80e0d65f0b7348dff08c2e89a23022e27cb9606155dce", first.GetProperty("signature").GetString());
        Assert.Equal(207, Encoding.UTF8.GetByteCount(first.GetProperty("body").GetString()!));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
