using System.Reflection;
using Mustask.Api.ErrorHandling;
using Mustask.Api.ErrorHandling.Catalog;

namespace Mustask.Contracts.Tests;

/// <summary>
/// Единая точка загрузки продуктового каталога и списка сборок <c>src/</c>
/// для этого проекта проверок. Каталог читается напрямую с диска (не через
/// встроенные ресурсы Mustask.Api) — так тест каталога не зависит от механизма
/// встраивания, а сам механизм отдельно проверяет побайтовое совпадение
/// (см. <see cref="CatalogLocationAndEmbeddingTests"/>).
/// </summary>
internal static class CatalogFixture
{
    /// <summary>Продуктовый каталог, разобранный из файлов репозитория.</summary>
    internal static ErrorCatalog Catalog { get; } = ErrorCatalogLoader.Load(
        RepositoryLayout.CatalogFiles().Select(file => new ErrorCatalogSourceFile(file.Name, File.ReadAllText(file.FullName))));

    /// <summary>
    /// Сборки <c>src/</c>, в которых могут жить <c>[ErrorCodes]</c>-классы
    /// (ADR-0005, п. 5): Domain и Application бросают исключения слайсов,
    /// Api — платформенный код (две константы, <c>GlobalExceptionHandler</c>
    /// и <c>PlatformErrorCodes</c>, намеренно без <c>[ErrorCodes]</c>: их
    /// присутствие в каталоге проверяет
    /// <c>PlatformErrorCodes.EnsureRequiredCodesArePresent</c> на старте),
    /// Shared — сами примитивы. Infrastructure сознательно
    /// не входит: по ADR-0004 исключения бросают Domain/Application, а
    /// лишняя ссылка протащила бы в этот проект EF Core.
    /// </summary>
    internal static IReadOnlyList<Assembly> SourceAssemblies { get; } =
    [
        typeof(Mustask.Shared.Errors.ErrorCodesAttribute).Assembly,
        Assembly.Load("Mustask.Domain"),
        Assembly.Load("Mustask.Application"),
        typeof(ErrorHandlingExtensions).Assembly,
    ];

    /// <summary>«Коды приложения» продукта — платформенные плюс объявленные константами (сегодня последних ноль, см. AppErrorCodeScanner).</summary>
    internal static IReadOnlyList<AppErrorCode> ApplicationCodes { get; } = AppErrorCodeScanner.Scan(SourceAssemblies);
}
