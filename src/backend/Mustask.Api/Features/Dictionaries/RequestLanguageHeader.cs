using Microsoft.Net.Http.Headers;

namespace Mustask.Api.Features.Dictionaries;

/// <summary>
/// `Accept-Language`: parses the header into an
/// ordered-by-quality list of tags and hands it to
/// `IRequestLanguageResolver` — `Mustask.Application` stays unaware of HTTP.
/// </summary>
public static class RequestLanguageHeader
{
    public static IReadOnlyList<string> ParseAcceptedTags(HttpRequest request)
    {
        var header = request.Headers.AcceptLanguage;
        if (header.Count == 0)
        {
            return [];
        }

        if (!StringWithQualityHeaderValue.TryParseList(header, out var values))
        {
            return [];
        }

        return values
            .OrderByDescending(v => v.Quality ?? 1.0)
            .Select(v => v.Value.ToString())
            .Where(v => v.Length > 0)
            .ToList();
    }

    /// <summary>Sets the response's `Content-Language` to the language the server resolved.</summary>
    public static void SetContentLanguage(HttpResponse response, string resolvedLanguageCode) =>
        response.Headers.ContentLanguage = resolvedLanguageCode;
}
