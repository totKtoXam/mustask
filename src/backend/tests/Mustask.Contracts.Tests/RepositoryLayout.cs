namespace Mustask.Contracts.Tests;

/// <summary>
/// Доступ к каталогу <c>contracts/</c> и корню репозитория. Тот же приём
/// поиска корня, что и в <c>Mustask.Architecture.Tests.RepositoryLayout</c>
/// (независимая копия — проекты не ссылаются друг на друга, а корень ищется
/// одинаково просто: вверх от <c>AppContext.BaseDirectory</c> до
/// <c>Mustask.sln</c>).
/// </summary>
internal static class RepositoryLayout
{
    private static readonly Lazy<DirectoryInfo> RootLazy = new(FindRoot);

    internal static DirectoryInfo Root => RootLazy.Value;

    internal static DirectoryInfo ContractsErrors => new(Path.Combine(Root.FullName, "contracts", "errors"));

    internal static DirectoryInfo Source => new(Path.Combine(Root.FullName, "src", "backend"));

    internal static DirectoryInfo Frontend => new(Path.Combine(Root.FullName, "src", "frontend"));

    /// <summary>
    /// Файлы каталога — все <c>*.json</c> в <c>contracts/errors/</c>, кроме
    /// <c>schema.json</c> (подсказка редактору, не файл модуля — у него нет
    /// <c>module</c>/<c>origin</c>/<c>codes</c>, contracts/README.md).
    /// </summary>
    internal static IReadOnlyList<FileInfo> CatalogFiles() =>
        ContractsErrors.EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
            .Where(file => !string.Equals(file.Name, "schema.json", StringComparison.Ordinal))
            .OrderBy(file => file.Name, StringComparer.Ordinal)
            .ToList();

    private static DirectoryInfo FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Mustask.sln")))
        {
            dir = dir.Parent;
        }

        return dir ?? throw new InvalidOperationException(
            $"Не найден корень репозитория (Mustask.sln) выше {AppContext.BaseDirectory}.");
    }
}
