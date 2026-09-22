using Mustask.Domain.Dictionaries;

namespace Mustask.Application.Abstractions.Dictionaries;

/// <summary>Чтение/запись <see cref="Language"/> для команд (CreateLanguage/UpdateLanguage/SetDefaultLanguage).</summary>
public interface ILanguageRepository
{
    Task<Language?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Language?> FindByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Все языки — источник для смены языка по умолчанию.</summary>
    Task<IReadOnlyList<Language>> GetAllAsync(CancellationToken cancellationToken);

    Task AddAsync(Language language, CancellationToken cancellationToken);

    /// <summary>Версия строки (`xmin`, ADR-0010) как непрозрачная строка для `version`/`If-Match`.</summary>
    string GetVersion(Language language);

    /// <summary>
    /// Фиксирует изменения этого агрегата. `DbUpdateConcurrencyException`
    /// (гонка внутри транзакции, ADR-0010) отображается в
    /// <see cref="Mustask.Shared.Exceptions.ConflictException"/> с кодом
    /// <see cref="DictionaryErrorCodes.VersionConflict"/> — та же реакция,
    /// что и у предварительной сверки версии в хендлере.
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken);
}
