namespace Mustask.Domain.Organizations;

/// <summary>
/// The fixed palette of 12 colours a department, a team and a project get when they are created (doc-016 BR-TD-20, ADR-0040). The
/// key is what is stored and returned; the hex belongs to the panel. The order is part of the rule: ties go to the earlier entry.
/// </summary>
public static class ColorPalette
{
    /// <summary>The keys in palette order, each with its hue in degrees on the 360° circle.</summary>
    public static IReadOnlyList<(string Key, int Hue)> Entries { get; } =
    [
        ("blue", 206),
        ("orange", 24),
        ("green", 146),
        ("purple", 272),
        ("red", 359),
        ("teal", 180),
        ("amber", 43),
        ("pink", 330),
        ("indigo", 238),
        ("lime", 76),
        ("magenta", 300),
        ("cyan", 195),
    ];

    /// <summary>The first entry: what an empty scope gets.</summary>
    public static string First => Entries[0].Key;

    public static bool IsKey(string? value) => Entries.Any(entry => entry.Key == value);

    /// <summary>The shortest distance between two hues on the 360° circle (0 to 180).</summary>
    public static int Distance(int first, int second)
    {
        var difference = Math.Abs(first - second) % 360;

        return Math.Min(difference, 360 - difference);
    }

    /// <summary>
    /// The colour a new object takes, from the colours <paramref name="inUse"/> in its scope (one entry per object using it, so a
    /// colour used twice is listed twice; a foreign key is ignored). With none in use, the first entry; while an entry is unused, the
    /// unused one whose smallest distance to the colours in use is largest, ties by palette order; when every entry is in use, the
    /// entry used by fewest, ties by palette order. Deterministic: the same input always gives the same key.
    /// </summary>
    public static string Choose(IEnumerable<string> inUse)
    {
        ArgumentNullException.ThrowIfNull(inUse);

        var counts = Entries.ToDictionary(entry => entry.Key, _ => 0);

        foreach (var key in inUse)
        {
            if (counts.ContainsKey(key))
            {
                counts[key]++;
            }
        }

        var usedHues = Entries.Where(entry => counts[entry.Key] > 0).Select(entry => entry.Hue).ToList();

        if (usedHues.Count == 0)
        {
            return First;
        }

        var unused = Entries.Where(entry => counts[entry.Key] == 0).ToList();

        if (unused.Count > 0)
        {
            var best = unused[0];
            var bestDistance = usedHues.Min(hue => Distance(hue, best.Hue));

            foreach (var entry in unused.Skip(1))
            {
                var distance = usedHues.Min(hue => Distance(hue, entry.Hue));

                if (distance > bestDistance)
                {
                    best = entry;
                    bestDistance = distance;
                }
            }

            return best.Key;
        }

        var fewest = counts.Values.Min();

        return Entries.First(entry => counts[entry.Key] == fewest).Key;
    }
}
