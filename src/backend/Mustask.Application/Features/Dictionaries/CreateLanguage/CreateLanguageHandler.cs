using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Dictionaries;
using Mustask.Domain.Dictionaries;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Dictionaries.CreateLanguage;

/// <summary>
/// `POST /api/dictionaries/languages`. The handler orchestrates (checks the code is free, persists); the domain
/// decides the shape of a freshly created entry -- always inactive with no
/// endonym required, until an administrator activates it through
/// `PUT /{code}` (<see cref="Language.Replace"/>).
/// </summary>
public sealed class CreateLanguageHandler(
    ILanguageRepository languages,
    IUnitOfWork unitOfWork,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<CreateLanguageResult> Handle(CreateLanguageCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (string.IsNullOrWhiteSpace(command.Code))
            {
                // An absent code is value_required on the
                // field, not the domain's invalid_code for an empty string.
                throw new ValidationException(
                    DictionaryErrorCodes.ValidationFailed,
                    new Dictionary<string, IReadOnlyList<FieldError>>
                    {
                        ["code"] = [new FieldError(DictionaryErrorCodes.ValueRequired)],
                    });
            }

            var now = clock.UtcNow;
            var author = currentUser.DisplayName;

            // Domain validates shape/length first (LanguageCode.Create inside
            // Language.Create) -- a malformed code or an over-length endonym
            // is reported on its own, before the uniqueness check below runs
            // at all.
            var language = Language.Create(Guid.CreateVersion7(), command.Code, command.Endonym, command.SortOrder ?? 0, now, author);

            if (await languages.FindByCodeAsync(language.Code, ct) is not null)
            {
                // 409, not a field-level 400 -- the
                // code itself is well-formed, it is simply taken already.
                throw new ConflictException(
                    DictionaryErrorCodes.LanguagesAlreadyExists,
                    new ErrorParams { ["code"] = ErrorParam.Text(language.Code) });
            }

            await languages.AddAsync(language, ct);
            await languages.SaveAsync(ct);

            return new CreateLanguageResult(ToResponse(language));
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
