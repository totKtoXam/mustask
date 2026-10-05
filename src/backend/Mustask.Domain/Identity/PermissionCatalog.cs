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

    /// <summary>
    /// Creating an organization (mod-002 BR-M1-2, D-i). Instance-level: it is
    /// held in the token, not granted by a membership, so it belongs here and
    /// not in <see cref="OrganizationPermissionCatalog"/>.
    /// </summary>
    public const string OrganizationsOrganizationCreate = "organizations.organization.create";

    /// <summary>Человекочитаемые описания кодов (диагностика, логи).</summary>
    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [DictionariesLanguagesRead] = "Просмотр справочника языков",
        [DictionariesLanguagesWrite] = "Изменение справочника языков",
        [OrganizationsOrganizationCreate] = "Создание организаций",
    };

    /// <summary>Все коды каталога — источник истины для realm-импорта.</summary>
    public static IReadOnlyCollection<string> AllCodes { get; } = Descriptions.Keys.ToList();
}
