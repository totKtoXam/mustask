namespace Mustask.Domain.Dictionaries;

/// <summary>
/// Коды языков, известные на старте (сид). Справочник языков открытый
/// (ADR-0021 §2) — администратор может добавить ещё; эти константы — не
/// закрытое перечисление, а имена, на которые ссылается код (например,
/// язык по умолчанию), тот же приём «класс констант рядом с сущностью»,
/// что и для будущих открытых справочников.
/// </summary>
public static class LanguageCatalog
{
    /// <summary>Русский — язык по умолчанию на старте.</summary>
    public const string Ru = "ru";

    public const string Kk = "kk";

    public const string En = "en";
}
