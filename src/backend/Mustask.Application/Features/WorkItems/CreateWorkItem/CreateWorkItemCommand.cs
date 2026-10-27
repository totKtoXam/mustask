using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Features.WorkItems.CreateWorkItem;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/workitems</c>
/// (US-1, api-contract § 2.2). Every value is the untrusted string it arrived as.
/// </summary>
/// <param name="RequestId">The client's key of this submission of the dialog (BR-M3-26).</param>
/// <param name="TypeId">A type of the organization's process (BR-M3-1).</param>
/// <param name="Title">Заголовок, untrimmed (BR-M3-3).</param>
/// <param name="Team">
/// mod-017 (BR-OT-2, BR-OT-3, api-contract § 0.2): the owning team the dialog shows — a team of the project, or <c>null</c>
/// inside the wrapper for «Без команды». <c>null</c> here is a field not sent: the item then takes the project default team
/// as it stands when it is saved. A request that names a parent never reads it.
/// </param>
/// <param name="ParentId">
/// feat-027 (BR-T-10, its api-contract § 3): the item to create the new one beneath; it is then linked beneath it, owned by the parent's team as stored then, else the project default
/// team (mod-017 BR-OT-3). <c>null</c> when the request has none.
/// </param>
public sealed record CreateWorkItemCommand(
    string? OrganizationId,
    string? ProjectId,
    string? RequestId,
    string? TypeId,
    string? Title,
    TeamChange? Team = null,
    string? ParentId = null);
