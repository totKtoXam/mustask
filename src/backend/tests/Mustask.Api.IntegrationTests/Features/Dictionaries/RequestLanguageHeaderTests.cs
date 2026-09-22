using Mustask.Api.Features.Dictionaries;
using Microsoft.AspNetCore.Http;

namespace Mustask.Api.IntegrationTests.Features.Dictionaries;

/// <summary>
/// `RequestLanguageHeader.ParseAcceptedTags` does the
/// quality-order sort itself before
/// `RequestLanguageResolver` ever sees the tags -- unit-level, no host and no
/// Docker, mirrors `BusinessExceptionHandlerTests`' use of
/// <see cref="DefaultHttpContext"/> in this same project.
/// </summary>
public sealed class RequestLanguageHeaderTests
{
    private static HttpRequest RequestWithAcceptLanguage(string? headerValue)
    {
        var context = new DefaultHttpContext();
        if (headerValue is not null)
        {
            context.Request.Headers.AcceptLanguage = headerValue;
        }

        return context.Request;
    }

    /// <summary>Tags come back ordered by explicit `q`, highest first.</summary>
    [Fact]
    public void ParseAcceptedTags_MultipleQualities_OrdersByQualityDescending()
    {
        var tags = RequestLanguageHeader.ParseAcceptedTags(RequestWithAcceptLanguage("kk;q=0.9,ru;q=0.8,fr;q=0.95"));

        Assert.Equal(["fr", "kk", "ru"], tags);
    }

    /// <summary>A tag with no explicit `q` defaults to 1.0 -- ahead of every explicitly-qualified one.</summary>
    [Fact]
    public void ParseAcceptedTags_TagWithoutQuality_DefaultsToHighestAndSortsFirst()
    {
        var tags = RequestLanguageHeader.ParseAcceptedTags(RequestWithAcceptLanguage("kk-KZ,kk;q=0.9,ru;q=0.8"));

        Assert.Equal(["kk-KZ", "kk", "ru"], tags);
    }

    /// <summary>No header at all -- an empty list, never a 500.</summary>
    [Fact]
    public void ParseAcceptedTags_NoHeader_ReturnsEmptyList()
    {
        var tags = RequestLanguageHeader.ParseAcceptedTags(RequestWithAcceptLanguage(headerValue: null));

        Assert.Empty(tags);
    }

    /// <summary>A wildcard header matches no code by construction -- it must not throw, and resolution falls through to the default elsewhere.</summary>
    [Fact]
    public void ParseAcceptedTags_Wildcard_DoesNotThrow()
    {
        var tags = RequestLanguageHeader.ParseAcceptedTags(RequestWithAcceptLanguage("*"));

        Assert.Equal(["*"], tags);
    }

    /// <summary>A header `StringWithQualityHeaderValue.TryParseList` cannot parse at all yields an empty list rather than throwing.</summary>
    [Fact]
    public void ParseAcceptedTags_MalformedHeader_ReturnsEmptyList_DoesNotThrow()
    {
        var tags = RequestLanguageHeader.ParseAcceptedTags(RequestWithAcceptLanguage(",,,;q=;;"));

        Assert.Empty(tags);
    }
}
