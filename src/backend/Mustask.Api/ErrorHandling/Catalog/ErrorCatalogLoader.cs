using System.Collections.Frozen;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Mustask.Shared.Errors;

namespace Mustask.Api.ErrorHandling.Catalog;

/// <summary>
/// Разбирает файлы каталога <c>contracts/errors/*.json</c> в
/// <see cref="ErrorCatalog"/>. Каждая структурная проблема — дубль кода,
/// неизвестная подсказка типа, пустой текст, отсутствующее обязательное
/// поле, неизвестное поле, дубль ключа внутри одного JSON-объекта, битый
/// JSON — валит загрузку исключением: каталог с такой проблемой не должен
/// дожить до обработки первого запроса. Каждое сообщение называет файл и,
/// где применимо, код — администратор продакшена должен понять, какой файл
/// чинить, не читая стек вручную. Более тонкие проверки (совпадение
/// плейсхолдеров с <c>params</c>,
/// побуквенное совпадение <c>kk</c> с <c>ru</c>, сироты) — предмет
/// <c>tests/Mustask.Contracts.Tests</c>, не загрузчика: они требуют знания о том,
/// какие коды реально объявлены в коде приложения, а загрузчик про это не
/// знает.
/// </summary>
public static class ErrorCatalogLoader
{
    /// <summary>
    /// Дубль ключа внутри одного JSON-объекта
    /// (например, две записи <c>"ru"</c> в одной записи кода) по умолчанию
    /// проглатывается — побеждает последняя. Дубль кода в пределах файла при
    /// этом уже ловился (<see cref="Load"/>, <c>TryAdd</c>) — без этой опции
    /// защищена была только половина случая.
    /// </summary>
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowDuplicateProperties = false,
    };

    private static readonly FrozenSet<string> RootProperties =
        new[] { "module", "origin", "codes" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Белый список зеркалит
    /// <c>contracts/errors/schema.json</c> (<c>additionalProperties: false</c>
    /// у записи кода), которую рантайм не читает, — опечатка вроде
    /// <c>"depricated"</c> иначе принимается молча, код остаётся «живым» и
    /// в справочнике, и в карте статусов, без единого предупреждения.
    /// </summary>
    private static readonly FrozenSet<string> EntryProperties =
        new[] { "statuses", "params", "ru", "kk", "deprecated", "kkNeedsReview" }.ToFrozenSet(StringComparer.Ordinal);

    public static ErrorCatalog Load(IEnumerable<ErrorCatalogSourceFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var entries = new Dictionary<string, ErrorCatalogEntry>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            foreach (var entry in ParseFile(file))
            {
                if (!entries.TryAdd(entry.Code, entry))
                {
                    throw new InvalidOperationException(
                        $"Код ошибки \"{entry.Code}\" объявлен более одного раза в каталоге " +
                        $"(повторно обнаружен в \"{file.Name}\").");
                }
            }
        }

        if (entries.Count == 0)
        {
            throw new InvalidOperationException(
                "Каталог ошибок пуст: не разобрано ни одной записи ни из одного файла.");
        }

        return new ErrorCatalog(entries);
    }

    /// <summary>
    /// Читает файлы каталога, встроенные в <paramref name="assembly"/> как
    /// <c>EmbeddedResource</c> с логическим именем, начинающимся на
    /// <paramref name="resourcePrefix"/>. Единственное место, где каталог
    /// читается из ресурсов сборки, — и только при старте приложения, не на
    /// пути запроса.
    /// </summary>
    public static ErrorCatalog LoadFromEmbeddedResources(Assembly assembly, string resourcePrefix)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcePrefix);

        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (resourceNames.Count == 0)
        {
            throw new InvalidOperationException(
                $"Не найдено ни одного встроенного ресурса с префиксом \"{resourcePrefix}\" " +
                $"в сборке {assembly.FullName}.");
        }

        var files = resourceNames.Select(name => ReadResource(assembly, name)).ToList();

        return Load(files);
    }

    private static ErrorCatalogSourceFile ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Не удалось открыть встроенный ресурс \"{resourceName}\".");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return new ErrorCatalogSourceFile(resourceName, reader.ReadToEnd());
    }

    private static IEnumerable<ErrorCatalogEntry> ParseFile(ErrorCatalogSourceFile file)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(file.Json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            // Без этой обёртки битый JSON давал
            // исключение только с LineNumber/BytePositionInLine — без имени
            // файла администратор не понимал, какой из файлов каталога
            // чинить.
            throw new InvalidOperationException(
                $"Файл \"{file.Name}\": не удалось разобрать JSON: {ex.Message}", ex);
        }

        using (document)
        {
            var root = document.RootElement;

            EnsureNoUnknownProperties(root, RootProperties, $"Файл \"{file.Name}\"");

            var module = RequireString(root, "module", file.Name);
            var origin = ParseOrigin(RequireString(root, "origin", file.Name), file.Name);

            if (!root.TryGetProperty("codes", out var codes) || codes.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Файл \"{file.Name}\": отсутствует объект \"codes\".");
            }

            var entries = new List<ErrorCatalogEntry>();

            foreach (var codeProperty in codes.EnumerateObject())
            {
                entries.Add(ParseEntry(codeProperty.Name, codeProperty.Value, module, origin, file.Name));
            }

            return entries;
        }
    }

    private static ErrorCatalogEntry ParseEntry(
        string code, JsonElement element, string module, ErrorCatalogOrigin origin, string fileName)
    {
        EnsureNoUnknownProperties(element, EntryProperties, $"Код \"{code}\" в файле \"{fileName}\"");

        if (!element.TryGetProperty("statuses", out var statusesElement) || statusesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Код \"{code}\" в файле \"{fileName}\": отсутствует массив \"statuses\".");
        }

        var statuses = ParseStatuses(code, statusesElement, fileName);
        var paramsDict = ParseParams(code, element, fileName);
        var ru = RequireNonEmptyText(element, "ru", code, fileName);
        var kk = RequireNonEmptyText(element, "kk", code, fileName);
        var deprecated = RequireOptionalBool(element, "deprecated", code, fileName);
        var kkNeedsReview = RequireOptionalBool(element, "kkNeedsReview", code, fileName);

        return new ErrorCatalogEntry(code, module, origin, statuses, paramsDict, ru, kk, deprecated, kkNeedsReview);
    }

    /// <summary>
    /// Раньше — прямой <c>GetInt32()</c>, и
    /// нечисловой элемент (например, <c>"409"</c> строкой) валил загрузку
    /// голым <c>InvalidOperationException</c> от <c>JsonElement</c> без
    /// имени файла и кода. <c>TryGetInt32</c> позволяет добавить оба.
    /// </summary>
    private static int[] ParseStatuses(string code, JsonElement statusesElement, string fileName)
    {
        var statuses = new List<int>(statusesElement.GetArrayLength());

        foreach (var status in statusesElement.EnumerateArray())
        {
            // TryGetInt32 сама по себе не спасает от голого
            // InvalidOperationException: она гарантированно не бросает
            // только при неудачном числовом преобразовании (переполнение,
            // дробная часть), но не при ValueKind, отличном от Number —
            // элемент-строка ("409" вместо 409) всё ещё валит её тем же
            // бестекстовым исключением, что и прямой GetInt32().
            if (status.ValueKind != JsonValueKind.Number || !status.TryGetInt32(out var value))
            {
                throw new InvalidOperationException(
                    $"Код \"{code}\" в файле \"{fileName}\": элемент массива \"statuses\" должен быть целым " +
                    $"числом, получено \"{status}\".");
            }

            statuses.Add(value);
        }

        return statuses.ToArray();
    }

    private static IReadOnlyDictionary<string, ErrorParamType> ParseParams(string code, JsonElement element, string fileName)
    {
        if (!element.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Код \"{code}\" в файле \"{fileName}\": отсутствует объект \"params\".");
        }

        var result = new Dictionary<string, ErrorParamType>(StringComparer.Ordinal);

        foreach (var property in paramsElement.EnumerateObject())
        {
            // Раньше — прямой GetString(), и нестроковое значение
            // (например, "used": 5) валило загрузку тем же голым
            // InvalidOperationException — без имени файла и кода. Проверка
            // ValueKind до чтения — тот же приём, что уже применён в
            // ParseStatuses/RequireOptionalBool.
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"Код \"{code}\" в файле \"{fileName}\": подсказка типа параметра " +
                    $"\"{property.Name}\" должна быть строкой.");
            }

            result[property.Name] = ParseParamType(property.Value.GetString(), code, property.Name, fileName);
        }

        return result;
    }

    private static ErrorParamType ParseParamType(string? hint, string code, string paramName, string fileName) => hint switch
    {
        "string" => ErrorParamType.Text,
        "number" => ErrorParamType.Number,
        "currency" => ErrorParamType.Currency,
        "date" => ErrorParamType.Date,
        "datetime" => ErrorParamType.DateTime,
        _ => throw new InvalidOperationException(
            $"Код \"{code}\" в файле \"{fileName}\": неизвестная подсказка типа \"{hint}\" у параметра \"{paramName}\"."),
    };

    private static ErrorCatalogOrigin ParseOrigin(string value, string fileName) => value switch
    {
        "platform" => ErrorCatalogOrigin.Platform,
        "slice" => ErrorCatalogOrigin.Slice,
        _ => throw new InvalidOperationException(
            $"Файл \"{fileName}\": неизвестное происхождение \"{value}\" (ожидалось \"platform\" или \"slice\")."),
    };

    private static string RequireString(JsonElement root, string propertyName, string fileName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Файл \"{fileName}\": отсутствует строковое поле \"{propertyName}\".");
        }

        return value.GetString()!;
    }

    private static string RequireNonEmptyText(JsonElement element, string propertyName, string code, string fileName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException(
                $"Код \"{code}\" в файле \"{fileName}\": поле \"{propertyName}\" пусто или отсутствует.");
        }

        return value.GetString()!;
    }

    /// <summary>
    /// Раньше — <c>&amp;&amp; deprecatedElement.GetBoolean()</c>,
    /// и нелогическое значение (например, строка <c>"yes"</c>) валило
    /// загрузку голым <c>InvalidOperationException</c> без имени файла и
    /// кода.
    /// </summary>
    private static bool RequireOptionalBool(JsonElement element, string propertyName, string code, string fileName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidOperationException(
                $"Код \"{code}\" в файле \"{fileName}\": поле \"{propertyName}\" должно быть булевым значением.");
        }

        return value.GetBoolean();
    }

    private static void EnsureNoUnknownProperties(JsonElement element, FrozenSet<string> allowedProperties, string context)
    {
        // ValueKind проверяется явно, а не через try/EnumerateObject: сама
        // эта проверка — новая, и заводить в ней ещё одну безымянную
        // ветку (EnumerateObject() бросает голый InvalidOperationException
        // для не-объекта) значило бы воспроизвести в свежем коде тот же
        // дефект — исключение без имени файла и кода.
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{context}: ожидался объект, получено {element.ValueKind}.");
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                throw new InvalidOperationException($"{context}: неизвестное поле \"{property.Name}\".");
            }
        }
    }
}
