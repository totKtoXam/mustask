using System.Xml.Linq;

namespace Mustask.Architecture.Tests;

/// <summary>
/// Доступ к файлам репозитория из тестов. Архитектурные правила проверяются
/// по исходникам и .csproj, а не только по скомпилированным сборкам: ссылка,
/// объявленная в проекте, но пока не использованная в коде, из метаданных
/// сборки не видна — а нарушением архитектуры уже является.
/// </summary>
internal static class RepositoryLayout
{
    private static readonly Lazy<DirectoryInfo> RootLazy = new(FindRoot);

    internal static DirectoryInfo Root => RootLazy.Value;

    internal static DirectoryInfo Source => new(Path.Combine(Root.FullName, "src", "backend"));

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

    /// <summary>Имена проектов, на которые ссылается <paramref name="project"/>.</summary>
    internal static IReadOnlyList<string> ProjectReferencesOf(string project)
    {
        var document = XDocument.Load(CsprojPath(project));

        return document.Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Имена NuGet-пакетов, подключённых в <paramref name="project"/>.</summary>
    internal static IReadOnlyList<string> PackageReferencesOf(string project)
    {
        var document = XDocument.Load(CsprojPath(project));

        return document.Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Все .cs файлы проекта, кроме сгенерированных (obj/, bin/).</summary>
    internal static IEnumerable<FileInfo> SourceFilesOf(string project)
    {
        var projectDirectory = new DirectoryInfo(Path.Combine(Source.FullName, project));

        if (!projectDirectory.Exists)
        {
            yield break;
        }

        foreach (var file in projectDirectory.EnumerateFiles("*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(projectDirectory.FullName, file.FullName)
                .Replace('\\', '/');

            if (relative.StartsWith("obj/", StringComparison.Ordinal) ||
                relative.StartsWith("bin/", StringComparison.Ordinal))
            {
                continue;
            }

            yield return file;
        }
    }

    private static string CsprojPath(string project)
    {
        var path = Path.Combine(Source.FullName, project, $"{project}.csproj");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Не найден проект {project} по пути {path}.", path);
        }

        return path;
    }
}
