using Mustask.Application.Features.Dictionaries.SetDefaultLanguage;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Domain.Dictionaries;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Dictionaries.SetDefaultLanguage;

/// <summary>
/// `PUT /api/dictionaries/languages/default`: the only slice touching two
/// <see cref="Language"/> aggregates (the old and the new default).
/// </summary>
public sealed class SetDefaultLanguageHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        FakeLanguageRepository Languages,
        FakeUnitOfWork UnitOfWork,
        FakeClock Clock,
        FakeCurrentUser CurrentUser,
        SetDefaultLanguageHandler Handler,
        Language DefaultLanguage,
        Language Target);

    private static Fixture NewFixture(bool targetActive = true)
    {
        var languagesRepo = new FakeLanguageRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock { UtcNow = Now };
        var currentUser = new FakeCurrentUser { DisplayName = "admin" };

        var defaultLanguage = Language.Seed(Guid.CreateVersion7(), "ru", "Русский", isDefault: true, isActive: true, 10, Now, "seed");
        var target = Language.Seed(Guid.CreateVersion7(), "kk", "Қазақша", isDefault: false, isActive: targetActive, 20, Now, "seed");

        languagesRepo.Add(defaultLanguage);
        languagesRepo.Add(target);

        var handler = new SetDefaultLanguageHandler(languagesRepo, unitOfWork, clock, currentUser);

        return new Fixture(languagesRepo, unitOfWork, clock, currentUser, handler, defaultLanguage, target);
    }

    /// <summary>An unknown code is not found -- the lookup shape every other language slice shares.</summary>
    [Fact]
    public async Task Handle_UnknownCode_ThrowsNotFound()
    {
        var f = NewFixture();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => f.Handler.Handle(
            new SetDefaultLanguageCommand("not-a-real-language"), CancellationToken.None));

        Assert.Equal(DictionaryErrorCodes.LanguagesNotFound, ex.Code);
    }

    /// <summary>Смена языка по умолчанию: новый становится языком по умолчанию, прежний теряет флаг.</summary>
    [Fact]
    public async Task Handle_ActiveTarget_MakesNewDefaultAndClearsOld()
    {
        var f = NewFixture();

        var result = await f.Handler.Handle(new SetDefaultLanguageCommand("kk"), CancellationToken.None);

        Assert.True(result.Response.IsDefault);
        Assert.True(f.Target.IsDefault);
        Assert.False(f.DefaultLanguage.IsDefault);
        // The new default stays active, ClearDefault does not touch the old
        // one's activity either (changing the default never deactivates
        // anyone).
        Assert.True(f.Target.IsActive);
        Assert.True(f.DefaultLanguage.IsActive);
        // Two flushes, not one -- see
        // Handle_MakesNewDefault_ClearsOldDefaultBeforeSettingTheNewOne for
        // why the order between them matters.
        Assert.Equal(2, f.Languages.SaveCallCount);
    }

    /// <summary>
    /// `ix_languages_is_default` is a
    /// plain partial unique index, checked per statement -- clearing the old
    /// default and flushing it must happen *before* the new default is set,
    /// regardless of which language's id would sort first for EF's own
    /// batching, or the two rows would both carry `is_default = true` for
    /// the duration of one flush. Asserted through the fake repository's
    /// save-order snapshots: after the first save, exactly the old default
    /// is `false`; only after the second is the new one `true`.
    /// </summary>
    [Fact]
    public async Task Handle_MakesNewDefault_ClearsOldDefaultBeforeSettingTheNewOne()
    {
        var f = NewFixture();

        await f.Handler.Handle(new SetDefaultLanguageCommand("kk"), CancellationToken.None);

        Assert.Equal(2, f.Languages.SaveSnapshots.Count);

        var afterFirstSave = f.Languages.SaveSnapshots[0].ToDictionary(x => x.Code, x => x.IsDefault);
        Assert.False(afterFirstSave["ru"]);
        Assert.False(afterFirstSave["kk"]);

        var afterSecondSave = f.Languages.SaveSnapshots[1].ToDictionary(x => x.Code, x => x.IsDefault);
        Assert.False(afterSecondSave["ru"]);
        Assert.True(afterSecondSave["kk"]);
    }

    /// <summary>Only an active language can become the default.</summary>
    [Fact]
    public async Task Handle_TargetInactive_ThrowsConflict_DefaultMustBeActive()
    {
        var f = NewFixture(targetActive: false);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => f.Handler.Handle(
            new SetDefaultLanguageCommand("kk"), CancellationToken.None));

        Assert.Equal(DictionaryErrorCodes.LanguagesDefaultMustBeActive, ex.Code);
        Assert.True(f.DefaultLanguage.IsDefault);
        Assert.Equal(0, f.Languages.SaveCallCount);
    }

    /// <summary>Assigning the language that already is the default is a no-op returning 200 -- nothing is saved.</summary>
    [Fact]
    public async Task Handle_AlreadyDefault_ReturnsOkNoOp()
    {
        var f = NewFixture();

        var result = await f.Handler.Handle(new SetDefaultLanguageCommand("ru"), CancellationToken.None);

        Assert.True(result.Response.IsDefault);
        Assert.False(f.Target.IsDefault);
        Assert.Equal(0, f.Languages.SaveCallCount);
    }

    /// <summary>The moment and the author of the change are stamped on both the new and the previous default.</summary>
    [Fact]
    public async Task Handle_MakesNewDefault_StampsBothLanguagesWithClockAndCurrentUser()
    {
        var f = NewFixture();
        var later = Now.AddHours(1);
        f.Clock.UtcNow = later;
        f.CurrentUser.DisplayName = "operator";

        await f.Handler.Handle(new SetDefaultLanguageCommand("kk"), CancellationToken.None);

        Assert.Equal(later, f.Target.UpdatedAt);
        Assert.Equal("operator", f.Target.UpdatedBy);
        Assert.Equal(later, f.DefaultLanguage.UpdatedAt);
        Assert.Equal("operator", f.DefaultLanguage.UpdatedBy);
    }

    /// <summary>The response carries the resolved `version`/audit fields -- same shape as the other language slices.</summary>
    [Fact]
    public async Task Handle_ReturnsResponseWithCodeAndVersion()
    {
        var f = NewFixture();

        var result = await f.Handler.Handle(new SetDefaultLanguageCommand("kk"), CancellationToken.None);

        Assert.Equal("kk", result.Response.Code);
        Assert.Equal(f.Languages.GetVersion(f.Target), result.Response.Version);
    }
}
