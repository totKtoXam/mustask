namespace Mustask.Application.Features.Organizations.GetRescueOrganizations;

/// <summary><c>GET /api/organizations/rescue?page=&amp;name=</c> (feat-033 api-contract § 1.1). Both are the untrusted strings that arrived.</summary>
/// <param name="Page">The page as sent: <c>null</c> for the first; anything but an integer of 1 or more is refused.</param>
/// <param name="Name">The name filter as typed; trimmed by the handler.</param>
public sealed record GetRescueOrganizationsQuery(string? Page, string? Name);
