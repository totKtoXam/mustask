namespace Mustask.AppHost.Provisioning;

/// <summary>
/// The rules of the stand reset (mod-014 D-10 (b); ADR-0025 § 7) that need no
/// Docker and no database — kept Aspire-free so the contract tests check them.
/// </summary>
public static class StandResetRules
{
    /// <summary>The AppHost flag that turns a start into a reset (<c>scripts/stand-reset.sh</c> passes it).</summary>
    public const string ResetFlag = "--stand-reset";

    /// <summary>Skips the confirmation.</summary>
    public const string YesFlag = "--yes";

    /// <summary>The databases of a PostgreSQL server a reset never drops, whatever the connection string names.</summary>
    public static readonly IReadOnlySet<string> ProtectedDatabases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "postgres", "template0", "template1",
    };

    /// <summary>
    /// Takes the reset's own flags out of the AppHost's arguments, so Aspire
    /// never sees them. Everything else passes through unchanged.
    /// </summary>
    public static StandResetArguments Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var reset = args.Contains(ResetFlag, StringComparer.Ordinal);
        var yes = args.Contains(YesFlag, StringComparer.Ordinal);
        var rest = args.Where(arg => arg != ResetFlag && arg != YesFlag).ToArray();

        if (yes && !reset)
        {
            throw new InvalidOperationException($"{YesFlag} only goes with {ResetFlag}.");
        }

        return new StandResetArguments(reset, yes, rest);
    }

    /// <summary>Whether a database may be dropped and recreated by the reset.</summary>
    public static bool MayReset(string database) =>
        !string.IsNullOrWhiteSpace(database) && !ProtectedDatabases.Contains(database.Trim());

    /// <summary>A PostgreSQL identifier, double-quoted, with inner quotes doubled.</summary>
    public static string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}

/// <summary>The AppHost's arguments with the reset's flags taken out.</summary>
public sealed record StandResetArguments(bool Reset, bool Yes, string[] AppHostArgs);
