using Mustask.Application.Features.Dictionaries.CreateLanguage;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Domain.Dictionaries;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Dictionaries.CreateLanguage;

/// <summary>Справочник языков: "добавление языка", "код языка уникален", "формат кода языка".</summary>
public sealed class CreateLanguageHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        FakeLanguageRepository Languages,
        FakeUnitOfWork UnitOfWork,
        FakeClock Clock,
        FakeCurrentUser CurrentUser,
        CreateLanguageHandler Handler);

    private static Fixture NewFixture()
    {
        var languagesRepo = new FakeLanguageRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock { UtcNow = Now };
        var currentUser = new FakeCurrentUser { DisplayName = "admin" };

        var handler = new CreateLanguageHandler(languagesRepo, unitOfWork, clock, currentUser);

        return new Fixture(languagesRepo, unitOfWork, clock, currentUser, handler);
    }

    /// <summary>"Добавление языка": появляется неактивным, без эндонима не требуется.</summary>
    [Fact]
    public async Task Handle_NewCode_CreatesInactiveLanguage()
    {
        var f = NewFixture();

        var result = await f.Handler.Handle(new CreateLanguageCommand("tr", "Türkçe", 40), CancellationToken.None);

        Assert.Equal("tr", result.Response.Code);
        Assert.Equal("Türkçe", result.Response.Endonym);
        Assert.False(result.Response.IsActive);
        Assert.False(result.Response.IsDefault);
        Assert.Equal(40, result.Response.SortOrder);
    }

    /// <summary>Contract: `code` is normalized -- stored lower-cased, trimmed.</summary>
    [Fact]
    public async Task Handle_MixedCaseCode_NormalizesToLowerCase()
    {
        var f = NewFixture();

        var result = await f.Handler.Handle(new CreateLanguageCommand(" TR ", null, null), CancellationToken.None);

        Assert.Equal("tr", result.Response.Code);
    }

    /// <summary>Contract: `sortOrder` — optional, default `0`.</summary>
    [Fact]
    public async Task Handle_SortOrderOmitted_DefaultsToZero()
    {
        var f = NewFixture();

        var result = await f.Handler.Handle(new CreateLanguageCommand("tr", null, null), CancellationToken.None);

        Assert.Equal(0, result.Response.SortOrder);
    }

    /// <summary>"Код языка уникален": повторное добавление того же кода отклоняется 409.</summary>
    [Fact]
    public async Task Handle_CodeAlreadyExists_ThrowsConflict()
    {
        var f = NewFixture();
        f.Languages.Add(Language.Seed(Guid.CreateVersion7(), "tr", "Türkçe", isDefault: false, isActive: true, 40, Now, "seed"));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => f.Handler.Handle(
            new CreateLanguageCommand("tr", "Something else", 50), CancellationToken.None));

        Assert.Equal(DictionaryErrorCodes.LanguagesAlreadyExists, ex.Code);
        Assert.Equal("tr", ex.Params["code"].Value);
    }

    /// <summary>Same as above but through case normalization -- "TR" collides with the already-seeded "tr".</summary>
    [Fact]
    public async Task Handle_CodeAlreadyExists_DifferentCase_ThrowsConflict()
    {
        var f = NewFixture();
        f.Languages.Add(Language.Seed(Guid.CreateVersion7(), "tr", "Türkçe", isDefault: false, isActive: true, 40, Now, "seed"));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => f.Handler.Handle(
            new CreateLanguageCommand("TR", null, null), CancellationToken.None));

        Assert.Equal(DictionaryErrorCodes.LanguagesAlreadyExists, ex.Code);
    }

    /// <summary>"Формат кода языка": длина вне 2–8 символов или не BCP-47 -- отклоняется до уникальности.</summary>
    [Theory]
    [InlineData("r")]
    [InlineData("ru_RU!")]
    [InlineData("toolongcode123")]
    public async Task Handle_InvalidCodeFormat_ThrowsValidation(string invalidCode)
    {
        var f = NewFixture();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => f.Handler.Handle(
            new CreateLanguageCommand(invalidCode, null, null), CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("code"));
        Assert.Equal(DictionaryErrorCodes.LanguagesInvalidCode, ex.Errors["code"][0].Code);
    }

    /// <summary>
    /// An absent or blank `code` is the
    /// generic field reason `dictionaries.value_required`, **not** the
    /// domain's `languages.invalid_code` for a malformed one — the handler
    /// guards this before `LanguageCode.Create` sees the value, and nothing
    /// pinned that guard down (the theory above only covers malformed
    /// non-empty codes).
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_BlankCode_ThrowsValidation_ValueRequired(string? blankCode)
    {
        var f = NewFixture();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => f.Handler.Handle(
            new CreateLanguageCommand(blankCode, "Türkçe", 40), CancellationToken.None));

        Assert.Equal(DictionaryErrorCodes.ValidationFailed, ex.Code);
        Assert.True(ex.Errors.ContainsKey("code"));
        Assert.Equal(DictionaryErrorCodes.ValueRequired, ex.Errors["code"][0].Code);
    }

    /// <summary>Contract: `endonym` ≤ 200 -- over the limit is `dictionaries.value_too_long`.</summary>
    [Fact]
    public async Task Handle_EndonymOverLengthLimit_ThrowsValidation()
    {
        var f = NewFixture();
        var tooLong = new string('a', DictionaryLimits.EndonymMaxLength + 1);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => f.Handler.Handle(
            new CreateLanguageCommand("tr", tooLong, null), CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("endonym"));
        Assert.Equal(DictionaryErrorCodes.ValueTooLong, ex.Errors["endonym"][0].Code);
    }

    /// <summary>The new language is queryable right away -- `GetAllAsync`/`FindByCodeAsync` see it in the same handler call.</summary>
    [Fact]
    public async Task Handle_NewCode_PersistsThroughTheRepository()
    {
        var f = NewFixture();

        await f.Handler.Handle(new CreateLanguageCommand("tr", "Türkçe", 40), CancellationToken.None);

        var persisted = await f.Languages.FindByCodeAsync("tr", CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(1, f.Languages.SaveCallCount);
    }

    [Fact]
    public async Task Handle_UsesClockAndCurrentUser_ForTheStamp()
    {
        var f = NewFixture();

        var result = await f.Handler.Handle(new CreateLanguageCommand("tr", "Türkçe", 40), CancellationToken.None);

        Assert.Equal(Now, result.Response.UpdatedAt);
        Assert.Equal("admin", result.Response.UpdatedBy);
    }
}
