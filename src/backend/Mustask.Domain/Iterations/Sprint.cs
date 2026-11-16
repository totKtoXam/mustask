using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Iterations;

/// <summary>
/// A team's sprint (mod-007 BR-1..BR-8). It belongs to one team for good and may hold items of any project; its number is the
/// team's own, 1, 2, 3 in creation order and never reused, and its state only moves Planned, Active, Closed. The aggregate
/// decides the lifecycle and the dates; the handler decides what it holds (the close, the removal).
/// </summary>
public sealed class Sprint
{
    private Sprint()
    {
        // EF materialization.
    }

    private Sprint(
        Guid id, Guid organizationId, Guid teamId, int number, SprintName name, SprintDates dates, DateTimeOffset createdAt, string createdBy)
    {
        Id = id;
        OrganizationId = organizationId;
        TeamId = teamId;
        Number = number;
        Name = name.Value;
        NormalizedName = name.Normalized;
        State = SprintState.Planned;
        StartDate = dates.Start;
        EndDate = dates.End;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid TeamId { get; private set; }

    /// <summary>The team's own number (BR-7).</summary>
    public int Number { get; private set; }

    public string Name { get; private set; } = null!;

    public string NormalizedName { get; private set; } = null!;

    public SprintState State { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTimeOffset? ClosedAt { get; private set; }

    public string? ClosedBy { get; private set; }

    public SprintDates Dates => new(StartDate, EndDate);

    /// <summary>A new Planned sprint with the number the caller took under the team's counter.</summary>
    public static Sprint Create(
        Guid organizationId, Guid teamId, int number, SprintName name, SprintDates dates, DateTimeOffset createdAt, string createdBy)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        if (organizationId == Guid.Empty || teamId == Guid.Empty)
        {
            throw new ArgumentException("A sprint belongs to a team of an organization.");
        }

        if (dates.Check() is not null)
        {
            throw new ArgumentException("The dates are checked before the sprint is built.", nameof(dates));
        }

        return new Sprint(Guid.CreateVersion7(), organizationId, teamId, number, name, dates, createdAt, createdBy);
    }

    /// <summary>
    /// Renames and dates the sprint (US-1, BR-4). A Planned sprint takes any acceptable pair, an Active one must keep both
    /// dates, a Closed one keeps its dates and takes only the name. The caller has checked the pair (<see cref="SprintDates.Check"/>).
    /// </summary>
    public void Update(SprintName name, SprintDates dates)
    {
        switch (State)
        {
            case SprintState.Closed when dates != Dates:
                throw new ConflictException(SprintErrorCodes.ClosedDatesLocked);
            case SprintState.Active when !dates.IsSet:
                throw new ConflictException(SprintErrorCodes.ActiveDatesRequired);
        }

        Name = name.Value;
        NormalizedName = name.Normalized;

        if (State != SprintState.Closed)
        {
            StartDate = dates.Start;
            EndDate = dates.End;
        }
    }

    /// <summary>Starts a Planned sprint that has both dates (BR-3, BR-4). The team's one Active sprint is the caller's and the index's.</summary>
    public void Start()
    {
        if (State != SprintState.Planned)
        {
            throw new ConflictException(SprintErrorCodes.NotPlanned);
        }

        if (!Dates.IsSet)
        {
            throw new ConflictException(SprintErrorCodes.StartDatesRequired);
        }

        State = SprintState.Active;
    }

    /// <summary>Closes the Active sprint (BR-6). Moving the items and writing the snapshot is the caller's, in the same transaction.</summary>
    public void Close(DateTimeOffset now, string closedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(closedBy);

        if (State != SprintState.Active)
        {
            throw new ConflictException(SprintErrorCodes.NotActive);
        }

        State = SprintState.Closed;
        ClosedAt = now;
        ClosedBy = closedBy;
    }
}
