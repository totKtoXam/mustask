using Mustask.Domain.UserDirectory;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.UserDirectory;

/// <summary>
/// QA re-check 3 (qa.md, D-5): a push that validation accepts must never fail when the projection derives its own
/// columns from it — the display name, the search name and e-mail, the sort key. A text that is accepted and then
/// throws is a 500 on the push channel, which the provider retries without end: the same stall the security pass's S1
/// and S2 closed for a lone surrogate and U+0000. Every Unicode scalar value is tried, in each field the derivation
/// reads. Fix-agnostic: a value validation refuses is fine, so is one that derives; one that does neither is the defect.
/// </summary>
public sealed class ProjectedUserTotalityQaTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private const long Moment = 1_790_000_000_000_000;

    private static IEnumerable<int> ScalarValues()
    {
        for (var codePoint = 0; codePoint <= 0x10FFFF; codePoint++)
        {
            if (codePoint is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            yield return codePoint;
        }
    }

    private static UserPushEntry Entry(string firstName, string lastName, string email) =>
        new("u-1", Moment, "u", firstName, lastName, email, Enabled: true, Deleted: false);

    [Fact]
    public void EveryScalarValueTheValidationAccepts_IsDerivable_InEveryField()
    {
        var failures = new List<string>();

        foreach (var codePoint in ScalarValues())
        {
            var text = char.ConvertFromUtf32(codePoint);

            foreach (var (field, entry) in new (string, UserPushEntry)[]
            {
                ("firstName", Entry($"A{text}B", "C", "e@example.test")),
                ("lastName", Entry("A", $"C{text}D", "e@example.test")),
                ("email", Entry("A", "C", $"e{text}@example.test")),
            })
            {
                try
                {
                    var pushes = UserPushValidation.Validate([entry], "changes", isSnapshotPage: false, Now);
                    var user = ProjectedUser.Create(pushes[0], Now);
                    _ = user.SortKey;
                }
                catch (ValidationException)
                {
                    // Refused at the door: nothing to derive, and a 400 is what the contract promises.
                }
                catch (Exception ex)
                {
                    failures.Add($"U+{codePoint:X4} in {field}: {ex.GetType().Name}");
                }
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} (scalar value, field) pairs pass validation and fail when derived: {string.Join("; ", failures.Take(10))}");
    }

    // A search is a read: a consumer's text with any scalar value in it must be answered or refused as invalid, never throw.
    [Fact]
    public void EveryScalarValue_AsASearchText_IsParsedWithoutThrowing()
    {
        var failures = new List<string>();

        foreach (var codePoint in ScalarValues())
        {
            try
            {
                _ = DirectorySearchText.Parse($"ab{char.ConvertFromUtf32(codePoint)}cd");
            }
            catch (Exception ex)
            {
                failures.Add($"U+{codePoint:X4}: {ex.GetType().Name}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} search texts throw: {string.Join("; ", failures.Take(10))}");
    }
}
