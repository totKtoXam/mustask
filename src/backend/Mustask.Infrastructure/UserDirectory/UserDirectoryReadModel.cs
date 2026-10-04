using System.Linq.Expressions;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.UserDirectory;

/// <summary>
/// <see cref="IUserDirectory"/> over <c>directory_users</c> (mod-014
/// api-contract.md A.3–A.6). One query per read, no tracking, projected
/// straight into the four values; never a call to Keycloak (A.7).
/// </summary>
public sealed class UserDirectoryReadModel(MustaskDbContext context) : IUserDirectory
{
    /// <summary>PostgreSQL's code-point collation for the tie-break on id (BR-UP-15).</summary>
    private const string CodePointCollation = "C";

    private static readonly Expression<Func<ProjectedUser, DirectoryUser>> Project =
        user => new DirectoryUser(user.Id, user.DisplayName, user.Email, user.IsActive);

    public async Task<UserSearchResult> SearchAsync(UserSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(search.Text);

        var text = DirectorySearchText.Parse(search.Text);
        var refusals = new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal);

        if (text.Kind == DirectorySearchTextKind.TooLong)
        {
            refusals["text"] = [LimitError(UserDirectoryErrorCodes.ValueTooLong, UserDirectoryLimits.SearchTextMaxLength)];
        }

        if (search.AmongIds is { Count: > UserDirectoryLimits.IdsPerRead })
        {
            refusals["amongIds"] = [LimitError(UserDirectoryErrorCodes.TooManyIds, UserDirectoryLimits.IdsPerRead)];
        }

        if (refusals.Count > 0)
        {
            throw new ValidationException(UserDirectoryErrorCodes.ValidationFailed, refusals);
        }

        if (text.Kind == DirectorySearchTextKind.TooShort || search.AmongIds is { Count: 0 })
        {
            // Never the whole directory for a short text; nobody for an empty restriction.
            return new UserSearchResult([], MoreMatched: false);
        }

        var pattern = text.LikePattern;
        var escape = DirectorySearchText.LikeEscape.ToString();

        var query = context.DirectoryUsers
            .AsNoTracking()
            .Where(user => EF.Functions.Like(user.SearchName, pattern, escape)
                || EF.Functions.Like(user.SearchEmail, pattern, escape));

        if (!search.IncludeInactive)
        {
            query = query.Where(user => user.IsActive);
        }

        if (search.AmongIds is { } amongIds)
        {
            var ids = DistinctIds(amongIds, nameof(search));
            query = query.Where(user => ids.Contains(user.Id));
        }

        var users = await query
            .OrderBy(user => user.SortKey)
            .ThenBy(user => EF.Functions.Collate(user.Id, CodePointCollation))
            .Take(UserDirectoryLimits.SearchAnswerSize + 1)
            .Select(Project)
            .ToListAsync(cancellationToken);

        var moreMatched = users.Count > UserDirectoryLimits.SearchAnswerSize;

        if (moreMatched)
        {
            users.RemoveAt(users.Count - 1);
        }

        return new UserSearchResult(users, moreMatched);
    }

    public async Task<UserLookupResult> LookupAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count > UserDirectoryLimits.IdsPerRead)
        {
            throw new ValidationException(
                UserDirectoryErrorCodes.ValidationFailed,
                new Dictionary<string, IReadOnlyList<FieldError>>
                {
                    ["ids"] = [LimitError(UserDirectoryErrorCodes.TooManyIds, UserDirectoryLimits.IdsPerRead)],
                });
        }

        var distinct = DistinctIds(ids, nameof(ids));

        if (distinct.Length == 0)
        {
            return new UserLookupResult(new Dictionary<string, DirectoryUser>(StringComparer.Ordinal), []);
        }

        var users = await context.DirectoryUsers
            .AsNoTracking()
            .Where(user => distinct.Contains(user.Id))
            .Select(Project)
            .ToListAsync(cancellationToken);

        var known = users.ToDictionary(user => user.Id, StringComparer.Ordinal);
        var unknown = distinct.Where(id => !known.ContainsKey(id)).ToArray();

        return new UserLookupResult(known, unknown);
    }

    private static string[] DistinctIds(IReadOnlyCollection<string> ids, string parameterName)
    {
        if (ids.Any(id => id is null))
        {
            throw new ArgumentException("An id in the list is null.", parameterName);
        }

        return ids.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static FieldError LimitError(string code, int limit) =>
        new(code, new ErrorParams { ["limit"] = ErrorParam.Number(limit) });
}
