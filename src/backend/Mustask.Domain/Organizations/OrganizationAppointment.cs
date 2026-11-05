namespace Mustask.Domain.Organizations;

/// <summary>
/// The record of one administrator appointment by a system administrator (feat-033 BR-RS-10): insert-only — it has a factory and
/// no mutator, no route edits or deletes it. The appointee and the appointer are Keycloak subjects, with no link to the user
/// projection (as <see cref="OrganizationMembership"/>); the record outlives any later role change or removal.
/// </summary>
public sealed class OrganizationAppointment
{
    private OrganizationAppointment()
    {
        // EF materialization.
    }

    private OrganizationAppointment(
        Guid id, Guid organizationId, string appointeeId, string appointedBy, DateTimeOffset appointedAt, string reason)
    {
        Id = id;
        OrganizationId = organizationId;
        AppointeeId = appointeeId;
        AppointedBy = appointedBy;
        AppointedAt = appointedAt;
        Reason = reason;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string AppointeeId { get; private set; } = null!;

    public string AppointedBy { get; private set; } = null!;

    public DateTimeOffset AppointedAt { get; private set; }

    /// <summary>The trimmed reason, 1 to 500 code points.</summary>
    public string Reason { get; private set; } = null!;

    public static OrganizationAppointment Record(
        Guid organizationId, string appointeeId, string appointedBy, AppointmentReason reason, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("An organization's id is never all zeros.", nameof(organizationId));
        }

        if (!OrganizationMembership.IsValidUserId(appointeeId))
        {
            throw new ArgumentException("The appointee is a Keycloak subject.", nameof(appointeeId));
        }

        if (!OrganizationMembership.IsValidUserId(appointedBy))
        {
            throw new ArgumentException("The appointer is a Keycloak subject.", nameof(appointedBy));
        }

        if (reason.Value is null)
        {
            throw new ArgumentException("The reason is required.", nameof(reason));
        }

        return new OrganizationAppointment(Guid.CreateVersion7(), organizationId, appointeeId, appointedBy, now, reason.Value);
    }
}
