namespace Mustask.Domain.UserDirectory;

/// <summary>
/// BR-UP-3: the display name is the first name and the last name, each
/// trimmed, empty ones dropped, joined by one space; when both are empty, the
/// username. Never empty for a held user, since the username is required.
/// </summary>
public static class DisplayNameRule
{
    public static string Compose(string? firstName, string? lastName, string username)
    {
        ArgumentNullException.ThrowIfNull(username);

        var first = (firstName ?? string.Empty).Trim();
        var last = (lastName ?? string.Empty).Trim();

        if (first.Length == 0 && last.Length == 0)
        {
            return username;
        }

        if (first.Length == 0)
        {
            return last;
        }

        return last.Length == 0 ? first : string.Concat(first, " ", last);
    }
}
