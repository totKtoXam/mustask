using Microsoft.Net.Http.Headers;

namespace Mustask.Api.Features.Dictionaries;

/// <summary>
/// `If-Match`: the client echoes the opaque
/// `version` string of a previous read, normally quoted as a strong ETag
/// (`"9134"`) per RFC 9110 — this strips the quotes so it compares equal to
/// the unquoted `version` a slice stores and reads back.
/// </summary>
public static class IfMatchHeader
{
    public static string? Parse(HttpRequest request)
    {
        var header = request.Headers.IfMatch;
        if (header.Count == 0)
        {
            return null;
        }

        var raw = header[0];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return EntityTagHeaderValue.TryParse(raw, out var tag) ? tag.Tag.ToString().Trim('"') : raw.Trim('"');
    }
}
