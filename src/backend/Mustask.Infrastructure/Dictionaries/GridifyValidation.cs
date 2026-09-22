using Gridify;
using Gridify.Syntax;
using Mustask.Domain.Dictionaries;
using Mustask.Shared.Errors;

namespace Mustask.Infrastructure.Dictionaries;

/// <summary>
/// ADR-0015: the two checks every list read model needs before handing a
/// query to Gridify, shared so the module's first list slice does not have
/// to invent them — `pageSize` outside its bounds and a `filter`/`orderBy`
/// field outside the mapper's whitelist are both a 400 with their own
/// top-level `errorCode`, not a silent truncation or skip, and not a field reason nested
/// under `dictionaries.validation_failed`.
/// </summary>
internal static class GridifyValidation
{
    internal const int MaxPageSize = 100;

    public static void EnsurePageSizeWithinLimit(IGridifyQuery query)
    {
        if (query.PageSize > MaxPageSize)
        {
            // Top-level errorCode, not a field reason under
            // validation_failed.
            throw new DictionaryQueryException(
                DictionaryErrorCodes.InvalidPageSize,
                new ErrorParams { ["limit"] = ErrorParam.Number(MaxPageSize) });
        }

        // pageSize<=0 and a negative page reach Gridify's
        // Skip/Take unchecked otherwise -- a negative LIMIT/OFFSET is a
        // PostgreSQL error, i.e. a plausible 500 from a client typo. The
        // contract defines no separate code for either bound, only
        // `dictionaries.invalid_page_size` for the pageSize ceiling, so both
        // low-side cases reuse it rather than inventing an unregistered code
        // (ADR-0005) -- it is still the most literally true description of
        // what is wrong with the request.
        if (query.PageSize <= 0)
        {
            throw new DictionaryQueryException(
                DictionaryErrorCodes.InvalidPageSize,
                new ErrorParams { ["limit"] = ErrorParam.Number(MaxPageSize) });
        }

        if (query.Page < 0)
        {
            throw new DictionaryQueryException(
                DictionaryErrorCodes.InvalidPageSize,
                new ErrorParams { ["limit"] = ErrorParam.Number(MaxPageSize) });
        }
    }

    /// <summary>
    /// A `filter`/`orderBy` value that Gridify's own
    /// parser cannot make sense of at all -- unbalanced parentheses, an
    /// unknown operator like `&gt;&gt;` -- used to reach the caller as a raw
    /// <see cref="GridifyFilteringException"/>/<see cref="GridifyOrderingException"/>/
    /// <see cref="GridifyMapperException"/> (none of them share a common base
    /// other than <see cref="Exception"/> itself) and answer 500. Parsing
    /// both up front, here, turns that into the module's own 400
    /// `dictionaries.invalid_query_syntax` -- distinct from
    /// `dictionaries.unknown_query_field` below, which is for a
    /// syntactically valid expression naming a field outside the whitelist.
    /// </summary>
    public static void EnsureFieldsAreWhitelisted<T>(IGridifyQuery query, IGridifyMapper<T> mapper)
    {
        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            try
            {
                foreach (var parsed in SyntaxTree.ParseFilterings(query.Filter))
                {
                    if (!mapper.HasMap(parsed.MemberName))
                    {
                        throw UnknownField(parsed.MemberName);
                    }
                }
            }
            catch (Exception ex) when (IsUnparsableGridifyExpression(ex))
            {
                throw InvalidSyntax("filter", ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.OrderBy))
        {
            try
            {
                foreach (var parsed in SyntaxTree.ParseOrderings(query.OrderBy))
                {
                    if (!mapper.HasMap(parsed.MemberName))
                    {
                        throw UnknownField(parsed.MemberName);
                    }
                }
            }
            catch (Exception ex) when (IsUnparsableGridifyExpression(ex))
            {
                throw InvalidSyntax("orderBy", ex);
            }
        }
    }

    /// <summary>
    /// The whitelist check above parses member names
    /// only -- an operator Gridify's own parser accepts syntactically but
    /// cannot evaluate (`?filter=code>>1`) is discovered only once
    /// `GridifyAsync` actually builds the expression tree. Both list read
    /// models call this instead of `GridifyAsync` directly, so the mapping to
    /// 400 stays in one place regardless of which of the two moments throws.
    /// </summary>
    public static async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> execute, IGridifyQuery query)
    {
        try
        {
            return await execute().ConfigureAwait(false);
        }
        catch (Exception ex) when (IsUnparsableGridifyExpression(ex))
        {
            var field = ex switch
            {
                GridifyOrderingException => "orderBy",
                _ => !string.IsNullOrWhiteSpace(query.Filter) ? "filter" : "orderBy",
            };

            throw InvalidSyntax(field, ex);
        }
    }

    // Gridify.GridifyFilteringException, .GridifyOrderingException and
    // .GridifyMapperException all derive directly from System.Exception (not
    // from a common Gridify base), so each is named explicitly rather than
    // caught through a shared ancestor.
    private static bool IsUnparsableGridifyExpression(Exception ex) =>
        ex is GridifyFilteringException or GridifyOrderingException or GridifyMapperException or GridifyQueryException;

    private static DictionaryQueryException InvalidSyntax(string field, Exception inner) => new(
        DictionaryErrorCodes.InvalidQuerySyntax,
        new ErrorParams { ["field"] = ErrorParam.Text(field) },
        inner);

    // Top-level errorCode, not a field reason under validation_failed. `params.field` carries the
    // unmapped member name only -- contracts/errors/dictionaries.json's
    // detail text ("Поле «{field}» нельзя использовать для фильтрации или
    // сортировки") does not distinguish `filter` from `orderBy`, so this
    // helper does not either.
    private static DictionaryQueryException UnknownField(string field) => new(
        DictionaryErrorCodes.UnknownQueryField,
        new ErrorParams { ["field"] = ErrorParam.Text(field) });
}
