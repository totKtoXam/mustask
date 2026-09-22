namespace Mustask.Api.ErrorHandling.Catalog;

/// <summary>Происхождение кода ошибки — колонка справочника <c>docs/error-codes.md</c>.</summary>
public enum ErrorCatalogOrigin
{
    /// <summary>Назначается обработчиком по HTTP-статусу; сам не бросается ни одним слайсом.</summary>
    Platform,

    /// <summary>Бросает конкретный слайс.</summary>
    Slice,
}
