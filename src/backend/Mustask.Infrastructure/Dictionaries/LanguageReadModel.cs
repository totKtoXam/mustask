using Gridify;
using Gridify.EntityFramework;
using Mustask.Application.Abstractions.Dictionaries;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Dictionaries;

/// <summary>
/// `GET /api/dictionaries/languages`. Справочник языков не переводится —
/// эндоним и есть собственное имя языка, резолвить по языку запроса нечего.
/// </summary>
public sealed class LanguageReadModel(MustaskDbContext context) : ILanguageReadModel
{
    private static readonly GridifyMapper<LanguageProjection> Mapper = BuildMapper();

    public async Task<Paging<LanguageRow>> GetPageAsync(IGridifyQuery query, CancellationToken cancellationToken)
    {
        GridifyValidation.EnsurePageSizeWithinLimit(query);
        GridifyValidation.EnsureFieldsAreWhitelisted(query, Mapper);

        var projected = context.Languages.AsNoTracking().Select(l => new LanguageProjection
        {
            Code = l.Code,
            Endonym = l.Endonym,
            IsDefault = l.IsDefault,
            IsActive = l.IsActive,
            SortOrder = l.SortOrder,
            UpdatedAt = l.UpdatedAt,
            UpdatedBy = l.UpdatedBy,
            Xmin = EF.Property<uint>(l, "xmin"),
        });

        // Default ordering (sortOrder, then the display text) applies
        // only when the caller did not ask for one.
        var ordered = string.IsNullOrWhiteSpace(query.OrderBy)
            ? projected.OrderBy(l => l.SortOrder).ThenBy(l => l.Endonym)
            : projected;

        var page = await GridifyValidation.ExecuteAsync(() => ordered.GridifyAsync(query, cancellationToken, Mapper), query);

        return new Paging<LanguageRow>(page.Count, page.Data.Select(ToRow).ToList());
    }

    private static LanguageRow ToRow(LanguageProjection p) => new(
        p.Code, p.Endonym, p.IsDefault, p.IsActive, p.SortOrder,
        p.Xmin.ToString(System.Globalization.CultureInfo.InvariantCulture), p.UpdatedAt, p.UpdatedBy);

    /// <summary>Filter/sort whitelist: `code`, `isActive`, `sortOrder`, `endonym`, `isDefault`.</summary>
    private static GridifyMapper<LanguageProjection> BuildMapper()
    {
        var mapper = new GridifyMapper<LanguageProjection>(autoGenerateMappings: false);

        mapper.AddMap("code", p => p.Code);
        mapper.AddMap("endonym", p => p.Endonym);
        mapper.AddMap("isDefault", p => p.IsDefault);
        mapper.AddMap("isActive", p => p.IsActive);
        mapper.AddMap("sortOrder", p => p.SortOrder);

        return mapper;
    }

    private sealed class LanguageProjection
    {
        public string Code { get; init; } = string.Empty;

        public string Endonym { get; init; } = string.Empty;

        public bool IsDefault { get; init; }

        public bool IsActive { get; init; }

        public int SortOrder { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }

        public string UpdatedBy { get; init; } = string.Empty;

        public uint Xmin { get; init; }
    }
}
