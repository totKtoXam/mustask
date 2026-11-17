using Mustask.Domain.Search;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Search.SearchStructure;

/// <summary>
/// The boundary of the read (feat-071 api-contract): the query is 2..100 characters after trimming and <c>expand</c> is null or one
/// of the five kinds. A refusal names the field and the reason and never echoes the typed text (BR-GS-10).
/// </summary>
public static class SearchStructureValidator
{
    public static (SearchQueryText Query, SearchGroupKind? Expand) Validate(SearchStructureQuery request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal);

        var kind = SearchQueryText.Judge(request.Query, out var query);

        if (kind != SearchQueryKind.Valid)
        {
            errors["query"] = [new FieldError(kind == SearchQueryKind.TooLong ? SearchErrorCodes.QueryTooLong : SearchErrorCodes.QueryTooShort)];
        }

        SearchGroupKind? expand = null;

        if (request.Expand is not null)
        {
            if (SearchGroupKinds.TryParse(request.Expand, out var parsed))
            {
                expand = parsed;
            }
            else
            {
                errors["expand"] = [new FieldError(SearchErrorCodes.InvalidGroup)];
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(SearchErrorCodes.ValidationFailed, errors);
        }

        return (query!, expand);
    }
}
