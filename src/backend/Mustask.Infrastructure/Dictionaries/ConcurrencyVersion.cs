using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Dictionaries;

/// <summary>
/// `version`/`If-Match` (ADR-0010): renders the
/// PostgreSQL system column `xmin` — the concurrency token every dictionary
/// entity gets automatically (<see cref="Conventions.OptimisticConcurrencyConvention"/>) —
/// as the opaque string the API hands to a client. ADR-0010 itself leaves
/// exposing `xmin` externally as "a separate decision" (see its
/// Consequences); this module's API contract is that decision,
/// made explicitly for this feature rather than changing the ADR's general
/// rule.
/// </summary>
internal static class ConcurrencyVersion
{
    public static string Read(DbContext context, object entity) =>
        ((uint)context.Entry(entity).Property("xmin").CurrentValue!).ToString(CultureInfo.InvariantCulture);

    public static bool TryParse(string? version, out uint value) =>
        uint.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
