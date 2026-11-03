using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mustask.Application.Abstractions.Boards;

/// <summary>
/// The opaque cursor of a group read (ADR-0036): base64url of a small structure — a fingerprint of the request it was issued for
/// (view, level, grouping, group, column and filters) and a position in the group's order. Forward only; not signed, since it
/// grants nothing (the caller's checks decide what is read). A malformed cursor, or one whose fingerprint differs from the
/// request's, does not decode: the handler answers the validation 400 on <c>cursor</c>.
/// </summary>
public static class GroupCursor
{
    private const int FingerprintBytes = 8;

    public static string Encode(CursorPosition position, string fingerprint)
    {
        ArgumentNullException.ThrowIfNull(position);

        var payload = position switch
        {
            ChangedPosition changed => new Payload { F = Hash(fingerprint), K = "c", T = changed.ChangedTicks, N = changed.Number, P = changed.ProjectKey },
            RankPosition rank => new Payload { F = Hash(fingerprint), K = "r", U = rank.Unplaced, L = rank.Place, T = rank.CreatedTicks, N = rank.Number },
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, "Unknown cursor position."),
        };

        return Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
    }

    /// <summary>The position in <paramref name="text"/>, or <c>false</c> when it is malformed or was issued for another request.</summary>
    public static bool TryDecode(string text, string fingerprint, bool rank, out CursorPosition position)
    {
        position = null!;

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(FromBase64Url(text));

            if (payload is null || payload.F != Hash(fingerprint))
            {
                return false;
            }

            if (rank && payload is { K: "r", U: 0 or 1, N: > 0 })
            {
                position = new RankPosition(payload.U, payload.L, payload.T, payload.N);

                return true;
            }

            if (!rank && payload is { K: "c", P: { Length: > 0 }, N: > 0 })
            {
                position = new ChangedPosition(payload.T, payload.N, payload.P);

                return true;
            }

            return false;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentException)
        {
            return false;
        }
    }

    private static string Hash(string fingerprint) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)).AsSpan(0, FingerprintBytes));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');

        return Convert.FromBase64String(padded);
    }

    private sealed class Payload
    {
        public string? F { get; set; }

        public string? K { get; set; }

        public int U { get; set; }

        public int L { get; set; }

        public long T { get; set; }

        public int N { get; set; }

        public string? P { get; set; }
    }
}
