namespace Mustask.Domain.WorkItems;

/// <summary>
/// The fixed limits of a work item (spec § Data model). None changes the
/// business without a release, so they are constants next to the entity that
/// owns them (docs/conventions/backend.md, "Named values").
/// </summary>
public static class WorkItemLimits
{
    /// <summary>The longest Заголовок, in UTF-16 code units, after trimming (BR-M3-3).</summary>
    public const int TitleMaxLength = 255;

    /// <summary>The longest plain Описание, in UTF-16 code units (BR-M3-19).</summary>
    public const int DescriptionMaxLength = 100_000;

    /// <summary>The longest visible text of an html Описание, in Unicode code points (feat-018 BR-RT-7, BR-RT-8).</summary>
    public const int DescriptionVisibleMaxLength = 100_000;

    /// <summary>The longest reduced markup of an html Описание, in Unicode code points (BR-RT-7).</summary>
    public const int DescriptionMarkupMaxLength = 1_000_000;

    /// <summary>The longest html Описание a request may carry, in UTF-16 code units, checked before any parsing (BR-RT-7).</summary>
    public const int DescriptionRequestMaxLength = 2_000_000;

    /// <summary>The rows of one page of a project's list (BR-M3-29).</summary>
    public const int ListPageSize = 50;

    /// <summary>The width of a user id: an opaque Keycloak subject (<c>sub</c>), as mod-014 stores it.</summary>
    public const int UserIdMaxLength = 255;

    /// <summary>The most options the «Родитель» picker answers at once (feat-001 spec § Screens and copy).</summary>
    public const int ParentCandidatesShown = 20;
}
