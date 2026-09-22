using Mustask.Application.Abstractions.Dictionaries;
using Mustask.Domain.Dictionaries;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Dictionaries;

public sealed class LanguageRepository(MustaskDbContext context) : ILanguageRepository
{
    public Task<Language?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Languages.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);

    /// <summary>
    /// The API stores and shows codes
    /// lower-cased (`LanguageCode.Create`); a route parameter is never run
    /// through that normalization on the way in, so `PUT /languages/RU`
    /// looked up `"RU"` verbatim and missed the stored `"ru"` row -- a false
    /// 404. Every write handler shares this one lookup, so normalizing here
    /// covers all of them at once rather than one line per call site.
    /// </summary>
    public Task<Language?> FindByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();

        return context.Languages.SingleOrDefaultAsync(l => l.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<Language>> GetAllAsync(CancellationToken cancellationToken) =>
        await context.Languages.ToListAsync(cancellationToken);

    public async Task AddAsync(Language language, CancellationToken cancellationToken) =>
        await context.Languages.AddAsync(language, cancellationToken);

    public string GetVersion(Language language) => ConcurrencyVersion.Read(context, language);

    /// <summary>
    /// ADR-0010: a genuine race inside the transaction (another write landed
    /// between the handler's own version pre-check and this call) surfaces
    /// here as <see cref="DbUpdateConcurrencyException"/> -- caught in the
    /// repository, which is allowed to know about EF Core, and translated
    /// into the module's own registry code so the Application handler never
    /// needs to reference the exception type itself.
    /// The same reasoning covers <c>CreateLanguageHandler</c>'s
    /// own free-code check racing a concurrent insert of the same code --
    /// `ix_languages_code`'s violation is the module's own
    /// `dictionaries.languages.already_exists`, the same code the
    /// pre-check throws for the non-racing case, so a client sees one
    /// contract-defined outcome either way.
    /// `languages` carries a second unique index,
    /// `ix_languages_is_default` (`LanguageConfiguration`, "exactly one
    /// default row"). A race on that one is not a duplicate code -- it is
    /// two writers disagreeing about which row is current, the same shape
    /// as the concurrency backstop above, so it maps to
    /// `dictionaries.version_conflict` instead.
    /// </summary>
    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException(DictionaryErrorCodes.VersionConflict, ErrorParams.None, ex);
        }
        catch (DbUpdateException ex) when (PostgresConflictDetection.IsUniqueViolationOnConstraint(ex, "ix_languages_code"))
        {
            throw new ConflictException(DictionaryErrorCodes.LanguagesAlreadyExists, ErrorParams.None, ex);
        }
        catch (DbUpdateException ex) when (PostgresConflictDetection.IsUniqueViolationOnConstraint(ex, "ix_languages_is_default"))
        {
            throw new ConflictException(DictionaryErrorCodes.VersionConflict, ErrorParams.None, ex);
        }
    }
}
