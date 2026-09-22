namespace Mustask.Architecture.Tests;

/// <summary>
/// Путь ошибки не зависит от БД, сети и чтения каталога в момент
/// обработки запроса (ADR-0006). HTTP-проверкой это не
/// фальсифицируется — ответ выглядит одинаково независимо от внутреннего
/// устройства, поэтому проверяется по
/// исходникам. Ссылка на Mustask.Api этому проекту не нужна — читаем файлы
/// напрямую через <see cref="RepositoryLayout"/>, как и остальные правила
/// этого проекта.
/// </summary>
public sealed class ErrorPathIndependence
{
    /// <summary>
    /// Запрещённые токены — признаки обращения к БД, сети или файловой
    /// системе. Простой поиск подстроки, не парсинг синтаксиса: этого
    /// достаточно, чтобы поймать явное использование, и с ним не нужно
    /// тащить Roslyn ради одной проверки.
    /// </summary>
    private static readonly string[] ForbiddenTokens =
    [
        "DbContext",
        "HttpClient",
        "File.",
        "Directory.",
        "StreamReader",
        "Npgsql",
    ];

    /// <summary>
    /// Единственное осознанное исключение: <c>ErrorCatalogLoader.cs</c>
    /// читает каталог из <c>EmbeddedResource</c> (через <c>StreamReader</c>
    /// над потоком встроенного ресурса, не файла на диске) один раз при
    /// старте приложения — вызывается из <c>AddMustaskErrorHandling</c>, не с
    /// пути запроса. «По построению файлов не читает»: поток берётся из
    /// <c>Assembly.GetManifestResourceStream</c>, а не из файловой системы.
    /// </summary>
    private const string CatalogLoaderFileName = "ErrorCatalogLoader.cs";

    [Fact]
    public void ErrorHandlingSources_DoNotReferenceExternalStateOnTheRequestPath()
    {
        var violations = new List<string>();

        foreach (var file in ErrorHandlingSourceFiles())
        {
            if (file.Name == CatalogLoaderFileName)
            {
                continue;
            }

            var code = StripLineComments(File.ReadAllText(file.FullName));

            foreach (var token in ForbiddenTokens)
            {
                if (code.Contains(token, StringComparison.Ordinal))
                {
                    violations.Add($"{file.Name}: содержит \"{token}\"");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Путь ошибки ссылается на внешнее состояние: " + string.Join(", ", violations));
    }

    /// <summary>
    /// Самопроверка невакуумности (зеркало <see cref="RulesAreNotVacuous"/>):
    /// исключённый файл существует и правда содержит один из запрещённых
    /// токенов — иначе исключение самого правила ничего не значило бы, а
    /// переименование файла тихо расширило бы проверку на код, который
    /// никогда не должен был подпадать под неё.
    /// </summary>
    [Fact]
    public void CatalogLoaderException_FileExistsAndActuallyContainsAForbiddenToken()
    {
        var file = ErrorHandlingSourceFiles().SingleOrDefault(f => f.Name == CatalogLoaderFileName);

        Assert.True(file is not null, $"{CatalogLoaderFileName} не найден в src/backend/Mustask.Api/ErrorHandling — исключение проверяет несуществующий файл.");

        var code = StripLineComments(File.ReadAllText(file!.FullName));

        Assert.Contains(ForbiddenTokens, token => code.Contains(token, StringComparison.Ordinal));
    }

    private static IEnumerable<FileInfo> ErrorHandlingSourceFiles()
    {
        var directory = new DirectoryInfo(Path.Combine(RepositoryLayout.Source.FullName, "Mustask.Api", "ErrorHandling"));

        return directory.Exists
            ? directory.EnumerateFiles("*.cs", SearchOption.AllDirectories)
            : [];
    }

    /// <summary>
    /// Убирает всё от <c>//</c> (в том числе <c>///</c>) до конца строки —
    /// без этого упоминание "NpgsqlException" в doc-комментарии
    /// (<c>OperationCanceledExceptionHandler</c>, объясняющем, почему
    /// обработчик разворачивает <c>InnerException</c>) ложно засчиталось бы
    /// нарушением. Строковые литералы с "//" внутри в этой папке не
    /// встречаются — наивного построчного среза достаточно, парсер не нужен.
    /// </summary>
    private static string StripLineComments(string source) =>
        string.Join('\n', source.Split('\n').Select(line =>
        {
            var index = line.IndexOf("//", StringComparison.Ordinal);
            return index < 0 ? line : line[..index];
        }));
}
