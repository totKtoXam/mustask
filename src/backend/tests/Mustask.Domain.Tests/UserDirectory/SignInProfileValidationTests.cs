using Mustask.Domain.UserDirectory;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.UserDirectory;

/// <summary>tech-025 (ADR-0034): the rules the claims of a sign-in meet before they are stored.</summary>
public sealed class SignInProfileValidationTests
{
    private static readonly SignInProfile Valid = new("sub-1", "a.smirnova", "Анна", "Смирнова", "anna@example.test");

    private static ValidationException Refusal(SignInProfile profile) =>
        Assert.Throws<ValidationException>(() => SignInProfileValidation.EnsureValid(profile));

    [Fact]
    public void EnsureValid_AllClaims_Passes() => SignInProfileValidation.EnsureValid(Valid);

    [Fact]
    public void EnsureValid_OnlyTheRequiredClaims_Passes() =>
        SignInProfileValidation.EnsureValid(new SignInProfile("sub-1", "a.smirnova", null, null, null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureValid_MissingId_IsRefusedAsRequired(string? id)
    {
        var ex = Refusal(Valid with { Id = id });

        Assert.Equal(UserDirectoryErrorCodes.ValidationFailed, ex.Code);
        Assert.Equal(UserDirectoryErrorCodes.ValueRequired, Assert.Single(ex.Errors[SignInProfileValidation.IdField]).Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EnsureValid_MissingUsername_IsRefusedAsRequired(string? username)
    {
        var ex = Refusal(Valid with { Username = username });

        Assert.Equal(UserDirectoryErrorCodes.ValueRequired, Assert.Single(ex.Errors[SignInProfileValidation.UsernameField]).Code);
    }

    [Fact]
    public void EnsureValid_EverythingMissing_NamesEveryRequiredFieldAtOnce()
    {
        var ex = Refusal(new SignInProfile(null, null, null, null, null));

        Assert.Equal([SignInProfileValidation.IdField, SignInProfileValidation.UsernameField], ex.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EnsureValid_FieldsAtTheLimit_Pass()
    {
        var atLimit = new string('я', UserDirectoryLimits.FieldMaxLength);

        SignInProfileValidation.EnsureValid(new SignInProfile(atLimit, atLimit, atLimit, atLimit, atLimit));
    }

    [Theory]
    [InlineData(SignInProfileValidation.IdField)]
    [InlineData(SignInProfileValidation.UsernameField)]
    [InlineData(SignInProfileValidation.FirstNameField)]
    [InlineData(SignInProfileValidation.LastNameField)]
    [InlineData(SignInProfileValidation.EmailField)]
    public void EnsureValid_OneOverTheLimit_IsRefusedAsTooLongWithTheLimit(string field)
    {
        var tooLong = new string('x', UserDirectoryLimits.FieldMaxLength + 1);
        var profile = field switch
        {
            SignInProfileValidation.IdField => Valid with { Id = tooLong },
            SignInProfileValidation.UsernameField => Valid with { Username = tooLong },
            SignInProfileValidation.FirstNameField => Valid with { FirstName = tooLong },
            SignInProfileValidation.LastNameField => Valid with { LastName = tooLong },
            _ => Valid with { Email = tooLong },
        };

        var reason = Assert.Single(Refusal(profile).Errors[field]);

        Assert.Equal(UserDirectoryErrorCodes.ValueTooLong, reason.Code);
    }

    // The limit counts code points, as Keycloak's column does: 255 astral characters are 510 UTF-16 units and pass.
    [Fact]
    public void EnsureValid_AstralCharactersAreCountedAsCodePoints()
    {
        var astral = string.Concat(Enumerable.Repeat("\U0001F600", UserDirectoryLimits.FieldMaxLength));

        SignInProfileValidation.EnsureValid(Valid with { FirstName = astral });
        Refusal(Valid with { FirstName = astral + "\U0001F600" });
    }

    [Theory]
    [InlineData("A\0B")]
    [InlineData("A￾B")]
    [InlineData("A﷐B")]
    [InlineData("A\U0001FFFFB")]
    public void EnsureValid_NulOrNoncharacter_IsRefusedAsInvalid(string text)
    {
        var ex = Refusal(Valid with { LastName = text, Email = text });

        Assert.Equal(UserDirectoryErrorCodes.ValueInvalid, Assert.Single(ex.Errors[SignInProfileValidation.LastNameField]).Code);
        Assert.Equal(UserDirectoryErrorCodes.ValueInvalid, Assert.Single(ex.Errors[SignInProfileValidation.EmailField]).Code);
    }

    [Theory]
    [InlineData("O'Brien-Müller")]
    [InlineData("Ёж — 山田 العربية")]
    [InlineData("a%b_c\\d")]
    [InlineData("<script>alert(1)</script>")]
    public void EnsureValid_OddButStorableText_Passes(string text) =>
        SignInProfileValidation.EnsureValid(Valid with { FirstName = text, LastName = text, Email = text });
}
