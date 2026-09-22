namespace Mustask.Architecture.Tests;

/// <summary>
/// Onion-часть архитектуры: зависимости направлены внутрь, к домену.
/// Правила из docs/conventions/backend.md.
/// </summary>
public sealed class OnionDependencyRules
{
    /// <summary>
    /// Слои и то, на какие проекты им разрешено ссылаться. Список закрытый:
    /// новая ссылка ломает тест, пока её осознанно не добавили сюда.
    /// </summary>
    public static TheoryData<string, string[]> AllowedProjectReferences => new()
    {
        { "Mustask.Shared", [] },
        { "Mustask.Domain", ["Mustask.Shared"] },
        { "Mustask.Application", ["Mustask.Domain", "Mustask.Shared"] },
        { "Mustask.Infrastructure", ["Mustask.Application", "Mustask.Domain", "Mustask.Shared"] },
        { "Mustask.Api", ["Mustask.Application", "Mustask.Domain", "Mustask.Infrastructure", "Mustask.ServiceDefaults", "Mustask.Shared"] },
    };

    [Theory]
    [MemberData(nameof(AllowedProjectReferences))]
    public void Layer_references_only_allowed_projects(string layer, string[] allowed)
    {
        var actual = RepositoryLayout.ProjectReferencesOf(layer);
        var forbidden = actual.Except(allowed, StringComparer.Ordinal).ToList();

        Assert.True(
            forbidden.Count == 0,
            $"{layer} ссылается на {string.Join(", ", forbidden)}. " +
            $"Разрешено: {(allowed.Length == 0 ? "ничего" : string.Join(", ", allowed))}. " +
            "Зависимости идут внутрь, к домену — см. docs/conventions/backend.md.");
    }

    [Theory]
    [InlineData("Mustask.Domain")]
    [InlineData("Mustask.Shared")]
    public void Domain_and_shared_have_no_nuget_dependencies(string layer)
    {
        var packages = RepositoryLayout.PackageReferencesOf(layer);

        Assert.True(
            packages.Count == 0,
            $"{layer} подключает пакеты: {string.Join(", ", packages)}. " +
            "Домен и общие примитивы остаются чистыми: бизнес-правила не зависят от библиотек.");
    }

    /// <summary>
    /// Инфраструктурные технологии не протекают в домен и в слой use case'ов.
    /// Проверка по .csproj ловит подключение пакета даже до первого using.
    /// </summary>
    [Theory]
    [InlineData("Mustask.Domain")]
    [InlineData("Mustask.Application")]
    [InlineData("Mustask.Shared")]
    public void Inner_layers_do_not_reference_infrastructure_packages(string layer)
    {
        string[] forbiddenPrefixes =
        [
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore",
            "Aspire",
            "Serilog",
        ];

        var violations = RepositoryLayout.PackageReferencesOf(layer)
            .Where(package => forbiddenPrefixes.Any(prefix =>
                package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"{layer} подключает инфраструктурные пакеты: {string.Join(", ", violations)}. " +
            "Абстракции объявляются в Mustask.Application/Abstractions, реализации живут в Mustask.Infrastructure.");
    }

    /// <summary>
    /// Тот же запрет, но по исходникам: пакет мог приехать транзитивно и
    /// оказаться доступен через using, не будучи объявленным в .csproj.
    /// </summary>
    [Theory]
    [InlineData("Mustask.Domain")]
    [InlineData("Mustask.Application")]
    [InlineData("Mustask.Shared")]
    public void Inner_layers_do_not_use_infrastructure_namespaces(string layer)
    {
        string[] forbiddenNamespaces =
        [
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore",
            "Mustask.Infrastructure",
            "Mustask.Api",
        ];

        var violations = new List<string>();

        foreach (var file in RepositoryLayout.SourceFilesOf(layer))
        {
            foreach (var line in File.ReadLines(file.FullName))
            {
                var trimmed = line.TrimStart();

                if (!trimmed.StartsWith("using ", StringComparison.Ordinal))
                {
                    continue;
                }

                if (forbiddenNamespaces.Any(ns =>
                        trimmed.Contains(ns, StringComparison.Ordinal)))
                {
                    violations.Add($"{file.Name}: {trimmed.TrimEnd()}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"{layer} использует инфраструктурные пространства имён:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }
}
