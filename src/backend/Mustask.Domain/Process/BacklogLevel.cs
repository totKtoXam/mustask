namespace Mustask.Domain.Process;

/// <summary>
/// A backlog level of a process (BR-M2-4): a name, a rank (a higher rank is a
/// higher level, unique in the process) and a kind.
/// </summary>
public sealed class BacklogLevel
{
    public const int NameMaxLength = 64;
    public const int KindMaxLength = 16;

    public Guid Id { get; private set; }

    public ProcessId ProcessId { get; private set; }

    public string Name { get; private set; } = null!;

    public int Rank { get; private set; }

    public BacklogLevelKind Kind { get; private set; }

    private BacklogLevel()
    {
        // EF materialization.
    }

    internal BacklogLevel(Guid id, ProcessId processId, string name, int rank, BacklogLevelKind kind)
    {
        Id = id;
        ProcessId = processId;
        Name = name;
        Rank = rank;
        Kind = kind;
    }
}
