using System.Text.RegularExpressions;

namespace Mustask.Architecture.Tests;

/// <summary>
/// Vertical-slice часть архитектуры: слайс самодостаточен и не тянется
/// к соседям. Именно эта граница ломается первой, когда «нужно переиспользовать
/// один хендлер» — общее выносится в домен или в Abstractions, а не берётся
/// напрямую из чужого слайса.
/// </summary>
public sealed partial class VerticalSliceRules
{
    private const string FeaturesRoot = "Features";

    [GeneratedRegex(@"^\s*using\s+(?:static\s+)?Mustask\.Application\.Features\.(?<module>\w+)\.(?<slice>\w+)",
        RegexOptions.ExplicitCapture)]
    private static partial Regex SliceUsing { get; }

    [Fact]
    public void Slice_does_not_reference_another_slice()
    {
        var violations = new List<string>();

        foreach (var file in RepositoryLayout.SourceFilesOf("Mustask.Application"))
        {
            var own = SliceOf(file);

            if (own is null)
            {
                continue;
            }

            foreach (var line in File.ReadLines(file.FullName))
            {
                var match = SliceUsing.Match(line);

                if (!match.Success)
                {
                    continue;
                }

                var referenced = $"{match.Groups["module"].Value}/{match.Groups["slice"].Value}";

                if (!string.Equals(referenced, own, StringComparison.Ordinal))
                {
                    violations.Add($"{own}: {file.Name} → {referenced}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Слайсы ссылаются друг на друга:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations) + Environment.NewLine +
            "Общее выносится в Mustask.Domain или в Mustask.Application/Abstractions.");
    }

    /// <summary>
    /// Хендлер живёт внутри своего слайса. Хендлер вне Features/ — признак
    /// того, что логика поехала обратно в горизонтальные слои.
    /// </summary>
    [Fact]
    public void Handlers_live_inside_a_slice()
    {
        var strays = RepositoryLayout.SourceFilesOf("Mustask.Application")
            .Where(file => file.Name.EndsWith("Handler.cs", StringComparison.Ordinal))
            .Where(file => SliceOf(file) is null)
            .Select(file => file.Name)
            .ToList();

        Assert.True(
            strays.Count == 0,
            $"Хендлеры вне слайса: {string.Join(", ", strays)}. " +
            "Ожидаемое место — Features/<Module>/<UseCase>/.");
    }

    /// <summary>
    /// Путь слайса относительно проекта: Features/&lt;Module&gt;/&lt;UseCase&gt;.
    /// Возвращает "Module/UseCase" либо null, если файл лежит вне слайсов.
    /// </summary>
    private static string? SliceOf(FileInfo file)
    {
        var projectDirectory = Path.Combine(RepositoryLayout.Source.FullName, "Mustask.Application");

        var segments = Path.GetRelativePath(projectDirectory, file.FullName)
            .Replace('\\', '/')
            .Split('/');

        return segments.Length >= 4 && segments[0] == FeaturesRoot
            ? $"{segments[1]}/{segments[2]}"
            : null;
    }
}
