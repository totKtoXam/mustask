using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Mustask.AppHost.Provisioning;

/// <summary>
/// The name of the stand's Keycloak store volume in external-database mode
/// (ADR-0025 § 7; D-10, D-12): <c>mustask-keycloak-</c> and the first 16 hex
/// digits of SHA-256 of <c>host|port|database</c>, read from
/// <c>ConnectionStrings:mustask</c>. Every checkout on that database shares
/// the store; stands on different databases never do. The loopback spellings
/// fold into one, the host's letter case is ignored, and no password, user
/// or other key enters the name.
/// </summary>
public static class StandStoreKey
{
    public const string ExternalPrefix = "mustask-keycloak-";

    private const int DefaultPort = 5432;

    private const int HexDigits = 16;

    private static readonly HashSet<string> Loopback = new(StringComparer.Ordinal)
    {
        "localhost", "127.0.0.1", "::1", "[::1]",
    };

    /// <summary>The volume name for an external database's connection string.</summary>
    public static string ForExternalDatabase(string connectionString)
    {
        var target = Target(connectionString);
        var key = string.Join('|', target.Host, target.Port.ToString(CultureInfo.InvariantCulture), target.Database);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

        return ExternalPrefix + hash[..HexDigits];
    }

    /// <summary>The database a connection string names — host (normalised), port, database; never the credentials.</summary>
    public static DatabaseTarget Target(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        var host = Value(builder, "Host") ?? Value(builder, "Server")
            ?? throw new InvalidOperationException("ConnectionStrings:mustask names no Host.");
        var database = Value(builder, "Database")
            ?? throw new InvalidOperationException("ConnectionStrings:mustask names no Database.");
        var portText = Value(builder, "Port");
        var port = DefaultPort;

        if (portText is not null && !int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
        {
            throw new InvalidOperationException("ConnectionStrings:mustask has a Port that is not a number.");
        }

        host = host.Trim().ToLowerInvariant();

        if (Loopback.Contains(host))
        {
            host = "localhost";
        }

        return new DatabaseTarget(host, port, database.Trim());
    }

    private static string? Value(DbConnectionStringBuilder builder, string key) =>
        builder.TryGetValue(key, out var value) && value?.ToString() is { Length: > 0 } text ? text : null;
}

/// <summary>Where the stand's database is, without the credentials.</summary>
public sealed record DatabaseTarget(string Host, int Port, string Database)
{
    public override string ToString() => FormattableString.Invariant($"{Host}:{Port}/{Database}");
}
