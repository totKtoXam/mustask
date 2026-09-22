using Mustask.Application.Features.Dictionaries.UpdateLanguage;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Domain.Dictionaries;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Dictionaries.UpdateLanguage;

/// <summary>
/// Активность записей справочника, справочник языков: изменение языка.
/// </summary>
public sealed class UpdateLanguageHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        FakeLanguageRepository Languages,
        FakeUnitOfWork UnitOfWork,
        FakeClock Clock,
        FakeCurrentUser CurrentUser,
        UpdateLanguageHandler Handler,
        Language DefaultLanguage,
        Language OtherLanguage);

    private static Fixture NewFixture()
    {
        var languagesRepo = new FakeLanguageRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock { UtcNow = Now };
        var currentUser = new FakeCurrentUser { DisplayName = "admin" };

        var defaultLanguage = Language.Seed(Guid.CreateVersion7(), "ru", "Русский", isDefault: true, isActive: true, 10, Now, "seed");
        var otherLanguage = Language.Create(Guid.CreateVersion7(), "tr", endonym: null, sortOrder: 40, Now, "seed");

        languagesRepo.Add(defaultLanguage);
        languagesRepo.Add(otherLanguage);

        var handler = new UpdateLanguageHandler(languagesRepo, unitOfWork, clock, currentUser);

        return new Fixture(languagesRepo, unitOfWork, clock, currentUser, handler, defaultLanguage, otherLanguage);
    }

    /// <summary>An unknown code -- 404.</summary>
    [Fact]
    public async Task Handle_UnknownCode_ThrowsNotFound()
    {
        var f = NewFixture();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => f.Handler.Handle(
            new UpdateLanguageCommand("not_a_real_code", "X", 10, true, "1"), CancellationToken.None));

        Assert.Equal(DictionaryErrorCodes.LanguagesNotFound, ex.Code);
    }

    /// <summary>The target already exists -- a missing `If-Match` is `version_required`, never "creates it" (this endpoint never creates one).</summary>
    [Fact]
    public async Task Handle_NoIfMatch_ThrowsValidationOnIfMatchField()
    {
        var f = NewFixture();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => f.Handler.Handle(
            new UpdateLanguageCommand("tr", "Türkçe", 40, true, null), CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("ifMatch"));
        Assert.Equal(DictionaryErrorCodes.VersionRequired, ex.Errors["ifMatch"][0].Code);
    }

    /// <summary>A mismatching `If-Match` -- 409.</summary>
    [Fact]
    public async Task Handle_MismatchedIfMatch_ThrowsConflict()
    {
        var f = NewFixture();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => f.Handler.Handle(
            new UpdateLanguageCommand("tr", "Türkçe", 40, true, "not-the-real-version"), CancellationToken.None));

        Assert.Equal(DictionaryErrorCodes.VersionConflict, ex.Code);
    }

    /// <summary>A matching `If-Match` replaces the mutable state (endonym, sortOrder, isActive) and returns it, `200`.</summary>
    [Fact]
    public async Task Handle_MatchingIfMatch_ReplacesAndReturnsUpdated()
    {
        var f = NewFixture();

        var result = await f.Handler.Handle(
            new UpdateLanguageCommand("tr", "Türkçe", 40, true, "1"), CancellationToken.None);

        Assert.Equal("Türkçe", result.Response.Endonym);
        Assert.Equal(40, result.Response.SortOrder);
        Assert.True(result.Response.IsActive);
        Assert.Equal(1, f.Languages.SaveCallCount);
    }

    /// <summary>"Активация языка без обязательных полей" -> для языка это отсутствие эндонима.</summary>
    [Fact]
    public async Task Handle_ActivateWithoutEndonym_ThrowsValidation_EndonymRequired()
    {
        var f = NewFixture();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => f.Handler.Handle(
            new UpdateLanguageCommand("tr", null, 40, true, "1"), CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("endonym"));
        Assert.Equal(DictionaryErrorCodes.LanguagesEndonymRequired, ex.Errors["endonym"][0].Code);
    }

    /// <summary>"Добавление языка" continued / "повторная активация": a non-empty endonym lets activation succeed.</summary>
    [Fact]
    public async Task Handle_ActivateWithEndonym_Succeeds()
    {
        var f = NewFixture();

        var result = await f.Handler.Handle(
            new UpdateLanguageCommand("tr", "Türkçe", 40, true, "1"), CancellationToken.None);

        Assert.True(result.Response.IsActive);
    }

    /// <summary>"Язык по умолчанию нельзя выключить".</summary>
    [Fact]
    public async Task Handle_DeactivateDefaultLanguage_ThrowsConflict()
    {
        var f = NewFixture();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => f.Handler.Handle(
            new UpdateLanguageCommand("ru", "Русский", 10, false, "1"), CancellationToken.None));

        Assert.Equal(DictionaryErrorCodes.LanguagesDefaultCannotBeDeactivated, ex.Code);
    }

    /// <summary>"Деактивация": a non-default language can be switched off.</summary>
    [Fact]
    public async Task Handle_DeactivateNonDefaultLanguage_Succeeds()
    {
        var f = NewFixture();
        var activated = await f.Handler.Handle(new UpdateLanguageCommand("tr", "Türkçe", 40, true, "1"), CancellationToken.None);

        var result = await f.Handler.Handle(
            new UpdateLanguageCommand("tr", "Türkçe", 40, false, activated.Response.Version), CancellationToken.None);

        Assert.False(result.Response.IsActive);
    }

    /// <summary>The endonym-required rule only binds an *active* language -- clearing it is allowed once the language is deactivated in the same call.</summary>
    [Fact]
    public async Task Handle_ClearEndonymWhileDeactivating_Succeeds()
    {
        var f = NewFixture();
        var activated = await f.Handler.Handle(new UpdateLanguageCommand("tr", "Türkçe", 40, true, "1"), CancellationToken.None);

        var result = await f.Handler.Handle(
            new UpdateLanguageCommand("tr", null, 40, false, activated.Response.Version), CancellationToken.None);

        Assert.Equal(string.Empty, result.Response.Endonym);
        Assert.False(result.Response.IsActive);
    }

    /// <summary>An absent `isActive` must not silently deactivate the language -- 400 `value_required` on that field.</summary>
    [Fact]
    public async Task Handle_MissingIsActive_ThrowsValidationOnIsActiveField()
    {
        var f = NewFixture();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => f.Handler.Handle(
            new UpdateLanguageCommand("tr", "Türkçe", 40, null, "1"), CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("isActive"));
        Assert.Equal(DictionaryErrorCodes.ValueRequired, ex.Errors["isActive"][0].Code);
    }

    /// <summary>An absent `sortOrder` must not silently reset it to 0 -- 400 `value_required` on that field.</summary>
    [Fact]
    public async Task Handle_MissingSortOrder_ThrowsValidationOnSortOrderField()
    {
        var f = NewFixture();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => f.Handler.Handle(
            new UpdateLanguageCommand("tr", "Türkçe", null, true, "1"), CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("sortOrder"));
        Assert.Equal(DictionaryErrorCodes.ValueRequired, ex.Errors["sortOrder"][0].Code);
    }

    /// <summary>Both fields absent are reported together.</summary>
    [Fact]
    public async Task Handle_MissingBothFields_ReportsBothAtOnce()
    {
        var f = NewFixture();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => f.Handler.Handle(
            new UpdateLanguageCommand("tr", "Türkçe", null, null, "1"), CancellationToken.None));

        Assert.True(ex.Errors.ContainsKey("isActive"));
        Assert.True(ex.Errors.ContainsKey("sortOrder"));
    }

    [Fact]
    public async Task Handle_UsesClockAndCurrentUser_ForTheStamp()
    {
        var f = NewFixture();

        var result = await f.Handler.Handle(
            new UpdateLanguageCommand("tr", "Türkçe", 40, true, "1"), CancellationToken.None);

        Assert.Equal(Now, result.Response.UpdatedAt);
        Assert.Equal("admin", result.Response.UpdatedBy);
    }
}
