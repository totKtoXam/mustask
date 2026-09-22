using System.Reflection;
using System.Text.Json;

namespace Mustask.Infrastructure.Dictionaries.Seeding;

/// <summary>One entry of `contracts/dictionaries/languages.json`.</summary>
public sealed record LanguageSeedEntry(string Code, string Endonym, bool IsDefault, bool IsActive, int SortOrder);

/// <summary>
/// Reads the seed files embedded from `contracts/dictionaries/` (not
/// `schema.json`, the editor hint). A malformed file fails loudly at
/// startup, the same discipline as `ErrorCatalogLoader` for
/// `contracts/errors/` -- a seed that cannot be parsed must not silently
/// leave the dictionary half-empty.
/// </summary>
public static class DictionarySeedFiles
{
    private const string ResourcePrefix = "Dictionaries/";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<LanguageSeedEntry> LoadLanguages(Assembly assembly)
    {
        var payload = ReadResource(assembly, "languages.json");
        var parsed = JsonSerializer.Deserialize<LanguagesFile>(payload, Options)
            ?? throw new InvalidOperationException("contracts/dictionaries/languages.json: пустой или некорректный JSON.");

        return parsed.Languages ?? throw new InvalidOperationException(
            "contracts/dictionaries/languages.json: отсутствует массив \"languages\".");
    }

    private static string ReadResource(Assembly assembly, string fileName)
    {
        var resourceName = ResourcePrefix + fileName;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Не найден встроенный ресурс \"{resourceName}\".");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);

        return reader.ReadToEnd();
    }

    private sealed record LanguagesFile(IReadOnlyList<LanguageSeedEntry>? Languages);
}
