namespace Mustask.Api.ErrorHandling.Catalog;

/// <summary>Один файл каталога (<c>contracts/errors/&lt;module&gt;.json</c>) перед разбором — имя для сообщений об ошибке плюс сырой текст.</summary>
public sealed record ErrorCatalogSourceFile(string Name, string Json);
