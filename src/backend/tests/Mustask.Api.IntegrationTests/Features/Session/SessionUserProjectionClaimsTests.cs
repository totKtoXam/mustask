using Mustask.Api.Features.Session;
using Mustask.Domain.UserDirectory;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>tech-025 (ADR-0034): how the id token becomes a profile and a fingerprint. No host and no database.</summary>
public sealed class SessionUserProjectionClaimsTests
{
    private static string Token(Action<KeycloakTestRealm.TokenOptions> configure) => KeycloakTestRealm.CreateToken(configure: configure);

    [Fact]
    public void ProfileOf_ReadsTheFiveClaims()
    {
        var token = Token(t =>
        {
            t.Subject = "sub-1";
            t.PreferredUsername = "a.smirnova";
            t.GivenName = "Анна";
            t.FamilyName = "Смирнова";
            t.Email = "anna@example.test";
        });

        Assert.Equal(new SignInProfile("sub-1", "a.smirnova", "Анна", "Смирнова", "anna@example.test"), SessionUserProjection.ProfileOf(token));
    }

    [Fact]
    public void ProfileOf_AbsentOptionalClaims_AreNull()
    {
        var profile = SessionUserProjection.ProfileOf(Token(t => t.Subject = "sub-1"));

        Assert.Null(profile.FirstName);
        Assert.Null(profile.LastName);
        Assert.Null(profile.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("a.b.c")]
    public void ProfileOf_UnreadableToken_GivesAnEmptyProfile(string? token) =>
        Assert.Equal(new SignInProfile(null, null, null, null, null), SessionUserProjection.ProfileOf(token));

    [Fact]
    public void FingerprintOf_SameClaims_SameValue_AnyChangedClaim_AnotherValue()
    {
        var profile = new SignInProfile("sub-1", "a.smirnova", "Анна", "Смирнова", "anna@example.test");
        var fingerprint = SessionUserProjection.FingerprintOf(profile);

        Assert.Equal(fingerprint, SessionUserProjection.FingerprintOf(profile with { }));
        Assert.Equal(32, fingerprint.Length);

        foreach (var changed in new[]
        {
            profile with { Id = "sub-2" },
            profile with { Username = "other" },
            profile with { FirstName = "Анн" },
            profile with { LastName = "Кузнецова" },
            profile with { Email = null },
        })
        {
            Assert.NotEqual(fingerprint, SessionUserProjection.FingerprintOf(changed));
        }
    }

    [Fact]
    public void FingerprintOf_DoesNotConfuseNeighbouringFields()
    {
        var one = new SignInProfile("sub-1", "ab", "c", null, null);
        var other = new SignInProfile("sub-1", "a", "bc", null, null);

        Assert.NotEqual(SessionUserProjection.FingerprintOf(one), SessionUserProjection.FingerprintOf(other));
    }
}
