using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Mustask.Application.Abstractions.RichText;
using Mustask.Infrastructure.RichText;

namespace Mustask.Infrastructure.Tests.RichText;

/// <summary>The reducer, the repository's shared fixture files and the parser round trip the rich-text tests share.</summary>
internal static class RichTextTestSupport
{
    public const int Ceiling = 1_000_000;

    public static readonly RichTextVocabulary Vocabulary = RichTextVocabulary.LoadEmbedded();

    public static readonly HtmlRichTextReducer Reducer = new(Vocabulary);

    public static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "p", "br", "h3", "h4", "strong", "em", "u", "s", "mark", "span", "code", "pre", "ul", "ol", "li", "a",
        "table", "tbody", "tr", "th", "td",
    };

    public static RichTextReduction Reduce(string html) => Reducer.Reduce(html, new RichTextReductionLimits(Ceiling), CancellationToken.None);

    /// <summary>What the handler stores for a value: the reduced html, or <c>""</c> when it has no content (BR-RT-6).</summary>
    public static string Stored(string html)
    {
        var reduction = Reduce(html);
        Assert.False(reduction.Exceeded, "The reduction was expected to stay within the bound.");

        return reduction.HasContent ? reduction.Html : string.Empty;
    }

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Mustask.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Mustask.sln not found above the test binaries.");
    }

    public static string ContractsFile(params string[] parts) =>
        Path.Combine([RepositoryRoot(), "contracts", "rich-text", .. parts]);

    /// <summary>The cases of a fixture file, each as its JSON object cloned out of the document.</summary>
    public static List<JsonElement> Cases(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractsFile("fixtures", file)));

        return document.RootElement.GetProperty("cases").EnumerateArray().Select(element => element.Clone()).ToList();
    }

    public static IEnumerable<object[]> CaseIds(string file) => Cases(file).Select(c => new object[] { c.GetProperty("id").GetString()! });

    public static JsonElement Case(string file, string id) =>
        Cases(file).Single(c => c.GetProperty("id").GetString() == id);

    /// <summary>Parses <paramref name="html"/> the way the reducer does, with scripting on or off, and returns the body.</summary>
    public static IElement ParseBody(string html, bool scripting)
    {
        var parser = new HtmlParser(new HtmlParserOptions { IsScripting = scripting });
        var document = parser.ParseDocument("<!DOCTYPE html><html><head></head><body>" + html);

        return document.Body!;
    }

    /// <summary>
    /// US-2 "A stored value is stable under a browser's re-parse": parsed again, serialised and reduced again, the
    /// value is itself, with scripting on and off.
    /// </summary>
    public static void AssertStableUnderReparse(string stored)
    {
        foreach (var scripting in new[] { true, false })
        {
            var serialised = ParseBody(stored, scripting).InnerHtml;

            Assert.Equal(stored, Stored(serialised));
        }
    }

    /// <summary>The stored value holds nothing outside the allow-list (US-2): elements, attributes, classes and addresses.</summary>
    public static void AssertInsideAllowList(string stored)
    {
        var body = ParseBody(stored, scripting: false);
        var classes = Vocabulary.Families.SelectMany(family => family.Tokens.Select(token => token.Class)).ToHashSet(StringComparer.Ordinal);

        foreach (var element in body.QuerySelectorAll("*"))
        {
            Assert.Contains(element.LocalName, AllowedElements);

            foreach (var attribute in element.Attributes)
            {
                var allowed = (element.LocalName, attribute.Name) switch
                {
                    ("a", "href") => LinkAddress.TryRead(attribute.Value, out var href) && href == attribute.Value,
                    ("a", "rel") => attribute.Value == "noopener noreferrer",
                    ("span" or "mark" or "code", "class") => classes.Contains(attribute.Value),
                    _ => false,
                };

                Assert.True(allowed, $"Attribute {attribute.Name}=\"{attribute.Value}\" on <{element.LocalName}> is outside the allow-list in: {stored}");
            }
        }
    }
}
