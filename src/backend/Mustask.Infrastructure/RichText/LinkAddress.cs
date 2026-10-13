namespace Mustask.Infrastructure.RichText;

/// <summary>
/// The link address rule of US-2: the address is read as a browser reads it (leading and trailing C0 controls and
/// spaces trimmed, ASCII tab, LF and CR removed anywhere; the parser has already decoded entities), and kept only if
/// it is <c>http</c> or <c>https</c> with a non-empty host, or <c>mailto</c> with a non-empty address. Relative,
/// protocol-relative, fragment-only, empty and every other scheme are refused. This is the rule; a library's scheme
/// check would be a second line.
/// </summary>
internal static class LinkAddress
{
    /// <summary>
    /// <c>true</c> with the address as the server stores it (<paramref name="href"/>: trimmed, tabs and line breaks
    /// removed, never re-encoded or resolved), or <c>false</c> when the link is to be removed and its text kept.
    /// </summary>
    public static bool TryRead(string? raw, out string href)
    {
        href = string.Empty;

        if (raw is null)
        {
            return false;
        }

        var start = 0;
        var end = raw.Length;

        while (start < end && raw[start] <= ' ')
        {
            start++;
        }

        while (end > start && raw[end - 1] <= ' ')
        {
            end--;
        }

        var cleaned = Remove(raw.AsSpan(start, end - start));

        var colon = cleaned.IndexOf(':');

        if (colon <= 0 || !IsSchemeStart(cleaned[0]))
        {
            return false;
        }

        for (var index = 1; index < colon; index++)
        {
            if (!IsSchemeCharacter(cleaned[index]))
            {
                return false;
            }
        }

        var scheme = cleaned[..colon];
        var rest = cleaned.AsSpan(colon + 1);

        if (scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            if (!HasHost(rest))
            {
                return false;
            }
        }
        else if (scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase))
        {
            if (!HasAddress(rest))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        href = cleaned;

        return true;
    }

    private static string Remove(ReadOnlySpan<char> text)
    {
        if (text.IndexOfAny('\t', '\n', '\r') < 0)
        {
            return text.ToString();
        }

        var builder = new System.Text.StringBuilder(text.Length);

        foreach (var c in text)
        {
            if (c is not ('\t' or '\n' or '\r'))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary><c>//authority</c> with a non-empty host; the authority holds no control, space or markup character.</summary>
    private static bool HasHost(ReadOnlySpan<char> rest)
    {
        if (rest.Length < 3 || rest[0] != '/' || rest[1] != '/')
        {
            return false;
        }

        var authority = rest[2..];
        var stop = authority.IndexOfAny('/', '?', '#');

        if (stop >= 0)
        {
            authority = authority[..stop];
        }

        foreach (var c in authority)
        {
            if (c <= ' ' || c == '\u007F' || c is '\\' or '<' or '>' or '"' or '`' or '{' or '}' or '|' or '^')
            {
                return false;
            }
        }

        var at = authority.LastIndexOf('@');

        if (at >= 0)
        {
            authority = authority[(at + 1)..];
        }

        // A port is not a host: ":80" has none.
        var host = authority.StartsWith("[") ? authority : authority[..(authority.IndexOf(':') is var colon and >= 0 ? colon : authority.Length)];

        return host.Length > 0;
    }

    /// <summary><c>mailto:</c> needs an address before any <c>?</c>, with no control or space in it.</summary>
    private static bool HasAddress(ReadOnlySpan<char> rest)
    {
        var query = rest.IndexOf('?');
        var address = query >= 0 ? rest[..query] : rest;

        if (address.Length == 0)
        {
            return false;
        }

        foreach (var c in address)
        {
            if (c <= ' ' || c == '\u007F')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSchemeStart(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static bool IsSchemeCharacter(char c) => IsSchemeStart(c) || c is >= '0' and <= '9' or '+' or '-' or '.';
}
