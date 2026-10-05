namespace Mustask.Application.Features.Organizations.CreateArea;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/areas</c> (US-5, US-6: create a child area, with or without an owner). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="ParentId">The parent's id as sent in the body.</param>
/// <param name="Name">The name as sent, untrimmed.</param>
/// <param name="OwnerTeamId">The owner's id as sent in the body; <c>null</c> to inherit.</param>
public sealed record CreateAreaCommand(string? OrganizationId, string? ProjectId, string? ParentId, string? Name, string? OwnerTeamId);
