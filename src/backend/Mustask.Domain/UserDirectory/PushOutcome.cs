namespace Mustask.Domain.UserDirectory;

/// <summary>What applying one entry did (api-contract.md B.4, the counts).</summary>
public enum PushOutcome
{
    /// <summary>The id was not held and now is.</summary>
    Added,

    /// <summary>A projected value or the status differs from the state held — not merely a newer moment.</summary>
    Changed,

    /// <summary>A duplicate, an older moment, or a newer moment with the same values and status.</summary>
    Unchanged,

    /// <summary>A state entry for an id held as deleted, ignored (BR-UP-5).</summary>
    HeldAsDeleted,
}
