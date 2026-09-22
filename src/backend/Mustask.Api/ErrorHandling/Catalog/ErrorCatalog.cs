using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Mustask.Shared.Errors;

namespace Mustask.Api.ErrorHandling.Catalog;

/// <summary>
/// Разобранный каталог ошибок — неизменяемый словарь код → запись,
/// построенный один раз при старте (<see cref="ErrorCatalogLoader"/>).
/// Регистрируется синглтоном; на пути обработки запроса — только чтение
/// этого словаря в памяти, ни файла, ни БД, ни сети (ADR-0006).
/// </summary>
public sealed class ErrorCatalog
{
    private readonly FrozenDictionary<string, ErrorCatalogEntry> _entries;

    /// <summary>
    /// Копирует переданный словарь в <see cref="FrozenDictionary{TKey,TValue}"/>
    /// с явным <see cref="StringComparer.Ordinal"/>: неизменяемость каталога обеспечена типом, а не
    /// договорённостью не мутировать источник после конструктора —
    /// вызывающий код (например, тестовый
    /// <c>ErrorCatalogTestData.MergedWithProductCatalog</c>) строит
    /// промежуточный <c>Dictionary</c> и мог бы продолжить его мутировать
    /// уже после регистрации синглтона; компаратор ключей теперь тоже
    /// гарантирован, а не унаследован от того, с чем построили источник.
    /// </summary>
    public ErrorCatalog(IReadOnlyDictionary<string, ErrorCatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _entries = entries.ToFrozenDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
    }

    /// <summary>Все записи каталога — источник для генератора справочника.</summary>
    public IReadOnlyCollection<ErrorCatalogEntry> Entries => _entries.Values.ToArray();

    public bool TryGet(string code, [NotNullWhen(true)] out ErrorCatalogEntry? entry) =>
        _entries.TryGetValue(code, out entry);

    public ErrorCatalogEntry Get(string code) =>
        TryGet(code, out var entry)
            ? entry
            : throw new KeyNotFoundException($"Код ошибки \"{code}\" отсутствует в каталоге.");

    /// <summary>
    /// Рендерит русский текст записи — единственный язык, который рендерит
    /// сервер; казахский текст существует в каталоге для справочника
    /// и будущего потребления клиентом, сервер его не
    /// рендерит в ответ.
    /// </summary>
    public ErrorRenderResult RenderRu(string code, ErrorParams parameters) =>
        ErrorTextRenderer.Render(Get(code).Ru, parameters);
}
