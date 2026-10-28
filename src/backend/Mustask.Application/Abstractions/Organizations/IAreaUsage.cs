namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// What other modules file under an area (BR-M1-13): M1 declares the question,
/// the module that owns those things answers it, so M1 never reads another
/// module's data. mod-003 implements it for work items, in any state, so items
/// in «Удалена» count once mod-005 ships (mod-003 BR-M3-24, api-contract § 7).
/// </summary>
/// <remarks>
/// The area deletion asks it after its <c>FOR UPDATE</c> lock on the area, as a
/// new statement under READ COMMITTED: a save that put an item in the area and
/// committed first is seen, and a save that comes second waits on the lock.
/// </remarks>
public interface IAreaUsage
{
    /// <summary>Whether anything of that project has <paramref name="areaId"/> as its area.</summary>
    Task<bool> IsInUseAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken);
}
