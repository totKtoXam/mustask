namespace Mustask.Domain.Identity;

/// <summary>
/// Каталог кодов прав, фиксированный в коде: источник истины, изменяется
/// только релизом. Состав ролей клиента <c>mustask-api</c> в realm-импорте
/// Keycloak сверяется с этим каталогом контрактными тестами.
/// </summary>
public static class PermissionCatalog
{
    /// <summary>Чтение справочника языков (выдаётся каждой панельной роли).</summary>
    public const string DictionariesLanguagesRead = "dictionaries.languages.read";

    /// <summary>Изменение справочника языков (создание, редактирование, назначение по умолчанию).</summary>
    public const string DictionariesLanguagesWrite = "dictionaries.languages.write";

    /// <summary>Человекочитаемые описания кодов (диагностика, логи).</summary>
    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [DictionariesLanguagesRead] = "Просмотр справочника языков",
        [DictionariesLanguagesWrite] = "Изменение справочника языков",
    };

    /// <summary>Все коды каталога — источник истины для realm-импорта.</summary>
    public static IReadOnlyCollection<string> AllCodes { get; } = Descriptions.Keys.ToList();
}
