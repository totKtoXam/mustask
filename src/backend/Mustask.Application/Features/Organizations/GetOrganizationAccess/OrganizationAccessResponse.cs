namespace Mustask.Application.Features.Organizations.GetOrganizationAccess;

/// <summary>The body of api-contract § 1.1: the caller's role in one organization and exactly its bundle of organization codes.</summary>
/// <param name="Role"><c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
/// <param name="Permissions">The role's organization codes in ordinal order. Never an instance code: those stay in the token.</param>
public sealed record OrganizationAccessResponse(string Role, IReadOnlyList<string> Permissions);
