namespace Mustask.Application.Features.Iterations.GetSprints;

/// <summary>
/// <c>GET …/teams/{teamId}/sprints?before=&amp;open=</c> (US-3). <paramref name="Before"/> is the cursor, the untrusted string it
/// arrived as; one that is not a number is no cursor.
/// </summary>
public sealed record GetSprintsQuery(string? OrganizationId, string? TeamId, string? Before, bool? Open);
