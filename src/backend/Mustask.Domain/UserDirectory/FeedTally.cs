namespace Mustask.Domain.UserDirectory;

/// <summary>
/// The counts of a push request or a snapshot (api-contract.md B.4):
/// <c>Added + Changed + Unchanged + HeldAsDeleted = Received</c>.
/// </summary>
public sealed record FeedTally(int Received, int Added, int Changed, int Unchanged, int HeldAsDeleted)
{
    public static FeedTally Empty { get; } = new(0, 0, 0, 0, 0);

    /// <summary>This tally with one more entry counted under <paramref name="outcome"/>.</summary>
    public FeedTally Count(PushOutcome outcome) => outcome switch
    {
        PushOutcome.Added => this with { Received = Received + 1, Added = Added + 1 },
        PushOutcome.Changed => this with { Received = Received + 1, Changed = Changed + 1 },
        PushOutcome.Unchanged => this with { Received = Received + 1, Unchanged = Unchanged + 1 },
        PushOutcome.HeldAsDeleted => this with { Received = Received + 1, HeldAsDeleted = HeldAsDeleted + 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown push outcome."),
    };

    public static FeedTally Sum(IEnumerable<FeedTally> tallies)
    {
        ArgumentNullException.ThrowIfNull(tallies);

        return tallies.Aggregate(Empty, (sum, tally) => new FeedTally(
            sum.Received + tally.Received,
            sum.Added + tally.Added,
            sum.Changed + tally.Changed,
            sum.Unchanged + tally.Unchanged,
            sum.HeldAsDeleted + tally.HeldAsDeleted));
    }
}
