using Mustask.Api.ErrorHandling;

namespace Mustask.Contracts.Tests;

/// <summary>
/// Каталог текстов не принадлежит фронтенду и не спрятан
/// внутри backend-проекта — он в нейтральном месте репозитория, а
/// backend-проверка полноты (см. <see cref="ErrorCatalogCompletenessTests"/>)
/// читает именно эти файлы, а не свою копию.
/// </summary>
public sealed class CatalogLocationAndEmbeddingTests
{
    [Fact]
    public void CatalogDirectory_IsOutsideFrontendAndSrc()
    {
        var contractsPath = RepositoryLayout.ContractsErrors.FullName.Replace('\\', '/');
        var frontendPath = RepositoryLayout.Frontend.FullName.Replace('\\', '/');
        var sourcePath = RepositoryLayout.Source.FullName.Replace('\\', '/');

        Assert.False(contractsPath.StartsWith(frontendPath, StringComparison.OrdinalIgnoreCase));
        Assert.False(contractsPath.StartsWith(sourcePath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Встроенные в Mustask.Api ресурсы побайтово равны файлам
    /// репозитория — сервер не читает файл в рантайме, но и
    /// не должен разъезжаться с ним содержимым застывшей копии.
    /// </summary>
    [Fact]
    public void EmbeddedResourcesInMustaskApi_AreByteEqualToRepositoryFiles()
    {
        var assembly = typeof(ErrorHandlingExtensions).Assembly;
        var files = RepositoryLayout.CatalogFiles();

        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var resourceName = ErrorHandlingExtensions.ErrorCatalogResourcePrefix + file.Name;
            using var stream = assembly.GetManifestResourceStream(resourceName);

            Assert.True(stream is not null, $"Встроенный ресурс \"{resourceName}\" не найден в Mustask.Api.dll.");

            using var memory = new MemoryStream();
            stream!.CopyTo(memory);

            var embeddedBytes = memory.ToArray();
            var repositoryBytes = File.ReadAllBytes(file.FullName);

            Assert.True(
                embeddedBytes.AsSpan().SequenceEqual(repositoryBytes),
                $"Встроенный ресурс \"{resourceName}\" расходится с файлом репозитория {file.FullName} побайтово.");
        }
    }
}
