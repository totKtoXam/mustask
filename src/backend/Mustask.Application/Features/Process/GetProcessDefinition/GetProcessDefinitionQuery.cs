namespace Mustask.Application.Features.Process.GetProcessDefinition;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/process</c>. The identifier is the
/// untrusted route value as it arrived: the handler judges it, so a malformed
/// value and an unknown one get the same answer (status.md D-9).
/// </summary>
public sealed record GetProcessDefinitionQuery(string? OrganizationId);
