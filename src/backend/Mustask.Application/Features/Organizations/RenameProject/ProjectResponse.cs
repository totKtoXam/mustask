namespace Mustask.Application.Features.Organizations.RenameProject;

/// <summary><c>Project</c> of api-contract § 2.3. The slice's own copy of the shape.</summary>
/// <param name="Key">2 to 10 Latin capital letters and digits; it never changes.</param>
public sealed record ProjectResponse(Guid Id, Guid OrganizationId, string Name, string Key);
