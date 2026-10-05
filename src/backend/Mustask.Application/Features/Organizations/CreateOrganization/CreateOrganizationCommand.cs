namespace Mustask.Application.Features.Organizations.CreateOrganization;

/// <summary>
/// <c>POST /api/organizations</c> (US-1: create an organization with its process (needs the create right)). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="Name">The name as sent, untrimmed.</param>
public sealed record CreateOrganizationCommand(string? Name);
