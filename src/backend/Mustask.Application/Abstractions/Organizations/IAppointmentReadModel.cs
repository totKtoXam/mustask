namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// Reads of feat-033: the «Все организации» list and an organization's appointment record, with the people named from the user
/// projection, never from Keycloak (mod-014 BR-UP-9). Statements are fixed whatever the size: the list is a count and a page; the
/// record is one read and <c>ceil(ids / 500)</c> lookups.
/// </summary>
public interface IAppointmentReadModel
{
    /// <summary>
    /// A page of every organization, active or deactivated (BR-RS-2), whose name contains <paramref name="nameFilter"/> (letter
    /// case ignored, <c>ё</c> = <c>е</c>; <c>null</c> = no filter), in name order (<c>ru-x-icu</c>, then id).
    /// </summary>
    Task<RescueOrganizationsPage> GetOrganizationsPageAsync(string? nameFilter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>The organization's records, newest first, the people named.</summary>
    Task<IReadOnlyList<AppointmentView>> ListAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>One record as <see cref="ListAsync"/> shows it; <c>null</c> when there is none.</summary>
    Task<AppointmentView?> FindAsync(Guid appointmentId, CancellationToken cancellationToken);

    /// <summary>Whether an organization of any status has this id.</summary>
    Task<bool> OrganizationExistsAsync(Guid organizationId, CancellationToken cancellationToken);
}

/// <param name="IsActive">Whether the organization is active (<c>false</c>: deactivated).</param>
/// <param name="LastAppointedAt"><c>null</c> while no appointment was made in it.</param>
public sealed record RescueOrganizationRowView(Guid Id, string Name, bool IsActive, DateTimeOffset? LastAppointedAt);

public sealed record RescueOrganizationsPage(int Count, IReadOnlyList<RescueOrganizationRowView> Rows);

/// <summary><c>Appointment</c> of feat-033 api-contract § 1.4.</summary>
public sealed record AppointmentView(Guid Id, DateTimeOffset AppointedAt, UserRefView AppointedBy, UserRefView Appointee, string Reason);
