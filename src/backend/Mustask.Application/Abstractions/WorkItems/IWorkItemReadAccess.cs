namespace Mustask.Application.Abstractions.WorkItems;

/// <summary>
/// "May this subject read this work item now": the item's own <c>GET</c> rule for a route that carries no organization (the
/// image read, feat-078 BR-IM-10 and -11). It answers what the item's read would — the organization active and the subject a
/// member, the project reachable, the item in any state, and <c>workitems.workitem.read</c> on the project with the grants that
/// reach it — read live, nothing cached; a parity test pins it to the item's read.
/// </summary>
public interface IWorkItemReadAccess
{
    Task<bool> MayReadAsync(Guid workItemId, string subjectId, CancellationToken cancellationToken);
}
