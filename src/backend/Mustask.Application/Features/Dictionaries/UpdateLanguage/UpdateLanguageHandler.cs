using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Dictionaries;
using Mustask.Domain.Dictionaries;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Dictionaries.UpdateLanguage;

/// <summary>
/// `PUT /api/dictionaries/languages/{code}`. The handler orchestrates
/// (loads the entry, applies the `If-Match` precondition, saves); the domain
/// decides (<see cref="Language.Replace"/> -- activation requires the
/// endonym, the default language cannot be deactivated).
/// </summary>
public sealed class UpdateLanguageHandler(
    ILanguageRepository languages,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<UpdateLanguageResult> Handle(UpdateLanguageCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var language = await languages.FindByCodeAsync(command.Code, ct)
                ?? throw new NotFoundException(DictionaryErrorCodes.LanguagesNotFound);

            // Checked BEFORE the domain mutates anything. The target here
            // always already exists (this endpoint never creates one), so a
            // missing `If-Match` is always `version_required`.
            EnsureVersionPrecondition(language, command.ExpectedVersion);

            // A full replace must not default an absent isActive/sortOrder.
            // Endonym is deliberately not in this check: absent/empty is a legal value
            // (Language.Replace normalizes it to ""), only activating with an
            // empty one is rejected, and that is a domain rule, not a
            // presence check.
            EnsureRequiredFieldsPresent(command);

            var now = clock.UtcNow;
            var author = currentUser.DisplayName;

            language.Replace(command.Endonym, command.SortOrder!.Value, command.IsActive!.Value, now, author);

            await languages.SaveAsync(ct);

            return new UpdateLanguageResult(ToResponse(language));
        }, cancellationToken);

    private void EnsureVersionPrecondition(Language language, string? expectedVersion)
    {
        if (expectedVersion is null)
        {
            // The target already exists and no If-Match was sent.
            throw new ValidationException(
                DictionaryErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>>
                {
                    ["ifMatch"] = [new FieldError(DictionaryErrorCodes.VersionRequired)],
                });
        }

        if (languages.GetVersion(language) != expectedVersion)
        {
            throw new ConflictException(DictionaryErrorCodes.VersionConflict);
        }
    }

    private static void EnsureRequiredFieldsPresent(UpdateLanguageCommand command)
    {
        var errors = new Dictionary<string, IReadOnlyList<FieldError>>();

        if (command.IsActive is null)
        {
            errors["isActive"] = [new FieldError(DictionaryErrorCodes.ValueRequired)];
        }

        if (command.SortOrder is null)
        {
            errors["sortOrder"] = [new FieldError(DictionaryErrorCodes.ValueRequired)];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(DictionaryErrorCodes.ValidationFailed, errors);
        }
    }

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
