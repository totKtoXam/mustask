using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Dictionaries;
using Mustask.Domain.Dictionaries;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Dictionaries.SetDefaultLanguage;

/// <summary>
/// `PUT /api/dictionaries/languages/default` -- the only slice that touches
/// two aggregates (the old and the new default <see cref="Language"/>). The
/// handler orchestrates the lookups; the domain decides through
/// <see cref="Language.AssertCanBecomeDefault"/> (only an active language
/// can become the default) and
/// <see cref="Language.MakeDefault"/>/<see cref="Language.ClearDefault"/>.
/// </summary>
public sealed class SetDefaultLanguageHandler(
    ILanguageRepository languages,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<SetDefaultLanguageResult> Handle(SetDefaultLanguageCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var target = await languages.FindByCodeAsync(command.Code ?? string.Empty, ct)
                ?? throw new NotFoundException(DictionaryErrorCodes.LanguagesNotFound);

            // Assigning the language that is already the default is a no-op
            // returning 200 -- checked before the precondition below so an
            // already-default language is never blocked by its own
            // (necessarily satisfied) preconditions.
            if (target.IsDefault)
            {
                return new SetDefaultLanguageResult(ToResponse(target));
            }

            // Only an active language can become the default.
            target.AssertCanBecomeDefault();

            var allLanguages = await languages.GetAllAsync(ct);
            var previousDefault = allLanguages.SingleOrDefault(l => l.IsDefault);

            var now = clock.UtcNow;
            var author = currentUser.DisplayName;

            // Both flips happen in the same transaction (the previous default
            // loses the flag in the same transaction) -- the storage-level
            // filtered unique index on `is_default` is the backstop, not the
            // mechanism.
            //
            // `ix_languages_is_default` is a plain (not DEFERRABLE) partial
            // unique index, so PostgreSQL checks it per statement, and EF Core orders the UPDATE
            // statements of one SaveChanges batch by primary key value, not
            // by call order here -- if the new default's id sorts before the
            // old one's, the "set true" statement would run before the
            // "clear old" statement and the index would see two defaults for
            // the duration of that one INSERT/UPDATE, a 500 rather than the
            // intended 200. Two separate SaveAsync calls inside this same
            // transaction make the order deterministic regardless of key
            // order: the old default is cleared and flushed to the database
            // first, so by the time the new default is set, at most one row
            // ever has `is_default = true`.
            previousDefault?.ClearDefault(now, author);
            await languages.SaveAsync(ct);

            target.MakeDefault(now, author);
            await languages.SaveAsync(ct);

            return new SetDefaultLanguageResult(ToResponse(target));
        }, cancellationToken);

    private LanguageResponse ToResponse(Language language) => new(
        language.Code,
        language.Endonym,
        language.IsDefault,
        language.IsActive,
        language.SortOrder,
        languages.GetVersion(language),
        language.UpdatedAt,
        language.UpdatedBy);
}
