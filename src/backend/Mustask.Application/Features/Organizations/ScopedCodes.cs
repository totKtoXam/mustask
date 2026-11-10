using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Features.Organizations;

/// <summary>Second codes decided on the objects a body names (mod-019 BR-SA-15, BR-SA-9): each id is judged on its own, an unknown or malformed one by the role alone.</summary>
internal static class ScopedCodes
{
    /// <summary>The id as the check sees it: the parsed GUID, or <see cref="Guid.Empty"/> for a malformed one (which names nothing).</summary>
    public static Guid Guess(string? raw) => AddressedId.TryParse(raw, out var id) ? id : Guid.Empty;

    /// <summary>Whether the caller holds <paramref name="code"/> on every object named; an empty list names nothing, so the role alone is judged (the 400 of the empty list follows for a role that may link at all).</summary>
    public static async Task<bool> HoldsOnAllAsync(
        IOrganizationAccess access, string code, IReadOnlyList<string?>? ids, CancellationToken cancellationToken)
    {
        if (ids is not { Count: > 0 })
        {
            return await access.HoldsOnAsync(code, Guid.Empty, cancellationToken);
        }

        foreach (var raw in ids ?? [])
        {
            if (!await access.HoldsOnAsync(code, Guess(raw), cancellationToken))
            {
                return false;
            }
        }

        return true;
    }
}
