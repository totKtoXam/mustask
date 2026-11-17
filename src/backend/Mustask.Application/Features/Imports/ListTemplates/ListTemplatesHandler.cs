using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Imports;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Templates;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Imports.ListTemplates;

/// <param name="Name">Substring filter, ignoring case.</param>
/// <param name="Author"><c>all</c> or <c>mine</c>.</param>
/// <param name="Sort"><c>name</c>, <c>-name</c>, <c>author</c>, <c>-author</c>, <c>updatedAt</c>, <c>-updatedAt</c>.</param>
public sealed record ListTemplatesQuery(string OrganizationId, string? Name, string? Author, string? Sort, int? Page, int? PageSize);

/// <summary>
/// The template list (api-contract § 2.2). An organization holds at most 100 templates, so the page is cut after the filter and the sort of
/// that set; the served flags come from the caller's access read once (BR-IE-18).
/// </summary>
public sealed class ListTemplatesHandler(
    IImportTemplateRepository templates, IUserDirectory directory, IOrganizationAccess access, ICurrentUser currentUser)
{
    private const int DefaultPageSize = 10;

    private const int MaxPageSize = 100;

    private static readonly string[] Sorts = ["name", "-name", "author", "-author", "updatedAt", "-updatedAt"];

    public async Task<TemplatePageView> Handle(ListTemplatesQuery query, CancellationToken cancellationToken)
    {
        var organizationId = ImportAddress.Organization(query.OrganizationId);
        var rights = await ImportRights.LoadAsync(access, currentUser.SubjectId, cancellationToken);

        var sort = string.IsNullOrEmpty(query.Sort) ? "name" : query.Sort;
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? DefaultPageSize;
        var author = string.IsNullOrEmpty(query.Author) ? "all" : query.Author;

        var errors = new Dictionary<string, IReadOnlyList<FieldError>>();

        if (!Sorts.Contains(sort, StringComparer.Ordinal))
        {
            errors["sort"] = [new FieldError(ImportErrorCodes.ValidationFailed)];
        }

        if (page < 1)
        {
            errors["page"] = [new FieldError(ImportErrorCodes.ValidationFailed)];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [new FieldError(ImportErrorCodes.ValidationFailed)];
        }

        if (author is not ("all" or "mine"))
        {
            errors["author"] = [new FieldError(ImportErrorCodes.ValidationFailed)];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(ImportErrorCodes.ValidationFailed, errors);
        }

        var all = await templates.ListAsync(organizationId, cancellationToken);
        var names = await ImportAddress.AuthorNamesAsync(directory, all.Select(t => t.AuthorId), cancellationToken);

        IEnumerable<ImportTemplate> filtered = all;

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var needle = query.Name.Trim();
            filtered = filtered.Where(t => t.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        if (author == "mine")
        {
            filtered = filtered.Where(t => t.AuthorId == rights.UserId);
        }

        string AuthorKey(ImportTemplate t) => names[t.AuthorId] ?? string.Empty;

        var ordered = sort switch
        {
            "-name" => filtered.OrderByDescending(t => t.Name, StringComparer.CurrentCultureIgnoreCase),
            "author" => filtered.OrderBy(AuthorKey, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase),
            "-author" => filtered.OrderByDescending(AuthorKey, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase),
            "updatedAt" => filtered.OrderBy(t => t.UpdatedAt),
            "-updatedAt" => filtered.OrderByDescending(t => t.UpdatedAt),
            _ => filtered.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase),
        };

        var list = ordered.ToList();
        var items = list.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new TemplateRowView(
                t.Id, t.Name, t.Version, new TemplateSourceBrief(t.Settings.Source.Format, t.Settings.Source.Sheet, t.Settings.Source.Encoding, t.Settings.Source.Delimiter),
                t.AuthorId, names[t.AuthorId], t.UpdatedAt, rights.CanChange(t), rights.CanChange(t), rights.CanCopy, rights.Run))
            .ToList();

        return new TemplatePageView(items, list.Count, page, pageSize, rights.CanCreate, rights.Run);
    }
}
