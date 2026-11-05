namespace Mustask.Application.Features.Organizations.GetRescueOrganizations;

/// <summary>The body of api-contract § 1.1 (ADR-0015's shape): the count of all pages and the rows of this one.</summary>
public sealed record RescueOrganizationsResponse(int Count, IReadOnlyList<RescueOrganizationRow> Data);

/// <summary>One organization: its identity, status and the moment of its newest appointment. Nothing else (BR-RS-2, BR-RS-3).</summary>
/// <param name="Status"><c>active</c> or <c>deactivated</c>.</param>
/// <param name="LastAppointedAt"><c>null</c> while no appointment was made.</param>
public sealed record RescueOrganizationRow(Guid Id, string Name, string Status, DateTimeOffset? LastAppointedAt);
