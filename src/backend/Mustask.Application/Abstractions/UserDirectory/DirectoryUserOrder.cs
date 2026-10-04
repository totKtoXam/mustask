using Mustask.Domain.UserDirectory;

namespace Mustask.Application.Abstractions.UserDirectory;

/// <summary>
/// BR-UP-15: the order of a search answer, offered to consumers for merging
/// batched answers (BR-UP-17) — by display name under the <c>ru</c> collation
/// at primary strength (<see cref="DirectoryCollation"/>, the same comparison
/// the stored sort key encodes), then by id in Unicode code-point order (what
/// PostgreSQL's <c>COLLATE "C"</c> gives on UTF-8). Total: a repeated search
/// answers the same users in the same order.
/// </summary>
public static class DirectoryUserOrder
{
    /// <summary>The size a merged answer is cut to — the read model's own answer size.</summary>
    internal const int SearchAnswerSize = UserDirectoryLimits.SearchAnswerSize;

    public static IComparer<DirectoryUser> Comparer { get; } = Comparer<DirectoryUser>.Create(Compare);

    private static int Compare(DirectoryUser? left, DirectoryUser? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        var byName = DirectoryCollation.Compare(left.DisplayName, right.DisplayName);

        return byName != 0 ? byName : CompareCodePoints(left.Id, right.Id);
    }

    /// <summary>
    /// Code point by code point — not <see cref="string.CompareOrdinal(string, string)"/>,
    /// which compares UTF-16 units and so puts a supplementary character
    /// before U+E000..U+FFFF, unlike UTF-8 bytes.
    /// </summary>
    private static int CompareCodePoints(string left, string right)
    {
        var leftRunes = left.EnumerateRunes();
        var rightRunes = right.EnumerateRunes();

        while (true)
        {
            var leftHas = leftRunes.MoveNext();
            var rightHas = rightRunes.MoveNext();

            if (!leftHas)
            {
                return rightHas ? -1 : 0;
            }

            if (!rightHas)
            {
                return 1;
            }

            var byRune = leftRunes.Current.Value.CompareTo(rightRunes.Current.Value);

            if (byRune != 0)
            {
                return byRune;
            }
        }
    }
}
