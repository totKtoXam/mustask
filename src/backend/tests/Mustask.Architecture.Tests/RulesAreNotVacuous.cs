namespace Mustask.Architecture.Tests;

/// <summary>
/// Самопроверка набора правил. Архитектурные тесты опасны тем, что зелёными
/// они бывают по двум причинам: нарушений нет — или проверка ничего не читает
/// (переехал каталог, сменилось имя проекта, не нашёлся корень репозитория).
/// Эти тесты отличают одно от другого: они падают, если механика правил
/// перестала видеть репозиторий.
/// </summary>
public sealed class RulesAreNotVacuous
{
    [Fact]
    public void Repository_root_is_found()
    {
        Assert.True(
            File.Exists(Path.Combine(RepositoryLayout.Root.FullName, "Mustask.sln")),
            $"Корень репозитория определён как {RepositoryLayout.Root.FullName}, но Mustask.sln там нет.");
    }

    [Theory]
    [InlineData("Mustask.Shared")]
    [InlineData("Mustask.Domain")]
    [InlineData("Mustask.Application")]
    [InlineData("Mustask.Infrastructure")]
    [InlineData("Mustask.Api")]
    public void Layer_project_is_readable(string layer)
    {
        var exception = Record.Exception(() => RepositoryLayout.ProjectReferencesOf(layer));

        Assert.True(
            exception is null,
            $"Правила не смогли прочитать {layer}: {exception?.Message}");
    }

    /// <summary>
    /// Известная существующая зависимость. Если парсер .csproj однажды начнёт
    /// возвращать пустой список, все правила Onion станут зелёными молча —
    /// этот тест упадёт первым.
    /// </summary>
    [Fact]
    public void Known_reference_is_detected()
    {
        var references = RepositoryLayout.ProjectReferencesOf("Mustask.Application");

        Assert.Contains("Mustask.Domain", references);
    }

    [Fact]
    public void Source_files_are_discovered()
    {
        var files = RepositoryLayout.SourceFilesOf("Mustask.Infrastructure").ToList();

        Assert.True(
            files.Count > 0,
            "В Mustask.Infrastructure не найдено ни одного .cs файла — правила по исходникам ничего не проверяют.");
    }

    /// <summary>
    /// ADR-0022: <see cref="SessionIsolationRules"/> фильтрует
    /// <c>Mustask.Api</c> по префиксу пути <c>Features/Session/</c> — переезд или
    /// переименование папки сделал бы обе его проверки зелёными вхолостую
    /// (пустая выборка), а не потому что нарушений нет.
    /// </summary>
    [Fact]
    public void Session_module_files_are_discovered()
    {
        var files = RepositoryLayout.SourceFilesOf("Mustask.Api")
            .Where(file => Path.GetRelativePath(
                    Path.Combine(RepositoryLayout.Source.FullName, "Mustask.Api"), file.FullName)
                .Replace('\\', '/')
                .StartsWith("Features/Session/", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            files.Count > 0,
            "В Features/Session/ не найдено ни одного .cs файла — SessionIsolationRules ничего не проверяет.");
    }

    /// <summary>Сгенерированные файлы не должны попадать в проверки по исходникам.</summary>
    [Fact]
    public void Generated_files_are_excluded()
    {
        var generated = RepositoryLayout.SourceFilesOf("Mustask.Infrastructure")
            .Where(file => file.FullName.Replace('\\', '/').Contains("/obj/", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            generated.Count == 0,
            $"В выборку попали сгенерированные файлы: {string.Join(", ", generated.Select(f => f.Name))}");
    }
}
