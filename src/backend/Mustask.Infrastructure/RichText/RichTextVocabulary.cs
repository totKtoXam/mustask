using System.Reflection;
using System.Text.Json;

namespace Mustask.Infrastructure.RichText;

/// <summary>The marks of the canonical form, in the order the vocabulary file names them (outermost first).</summary>
internal enum MarkKind
{
    Link,
    Bold,
    Italic,
    Underline,
    Strike,
    Highlight,
    TextColor,
    FontSize,
}

/// <summary>One entry of a closed vocabulary: the stored class and the name the editor and the theme know it by.</summary>
internal sealed record VocabularyToken(string Name, string Class);

/// <summary>A closed set of class tokens that belong to one element (ADR-0037 decision 6).</summary>
internal sealed class TokenFamily
{
    private readonly Dictionary<string, int> byClass;

    public TokenFamily(string kind, string element, string? within, IReadOnlyList<VocabularyToken> tokens)
    {
        Kind = kind;
        Element = element;
        Within = within;
        Tokens = tokens;
        byClass = new Dictionary<string, int>(tokens.Count, StringComparer.Ordinal);

        for (var index = 0; index < tokens.Count; index++)
        {
            byClass[tokens[index].Class] = index;
        }
    }

    public string Kind { get; }

    public string Element { get; }

    public string? Within { get; }

    public IReadOnlyList<VocabularyToken> Tokens { get; }

    /// <summary>
    /// The index of the first token of this family found in a <c>class</c> attribute value: it is split on ASCII
    /// whitespace only and each part is compared exactly and case-sensitively (BR-RT-14). <c>-1</c> when none is.
    /// </summary>
    public int FirstMatch(string? classValue)
    {
        if (string.IsNullOrEmpty(classValue))
        {
            return -1;
        }

        var span = classValue.AsSpan();
        var start = -1;

        for (var index = 0; index <= span.Length; index++)
        {
            var isSeparator = index == span.Length || IsAsciiWhitespace(span[index]);

            if (!isSeparator)
            {
                if (start < 0)
                {
                    start = index;
                }

                continue;
            }

            if (start >= 0)
            {
                // The lookup is by the exact part; a part is never normalised.
                if (byClass.TryGetValue(span[start..index].ToString(), out var found))
                {
                    return found;
                }

                start = -1;
            }
        }

        return -1;
    }

    private static bool IsAsciiWhitespace(char c) => c is ' ' or '\t' or '\n' or '\f' or '\r';
}

/// <summary>
/// <c>contracts/rich-text/vocabularies.json</c> as the server holds it (ADR-0037 decision 6): the only source of a
/// class token, embedded like the dictionaries. No token literal appears in server code; the reducer asks this table.
/// A file the reduction cannot implement fails the load, so the start fails, not a save.
/// </summary>
internal sealed class RichTextVocabulary
{
    public const string ResourceName = "RichText/vocabularies.json";

    private static readonly string[] KnownKinds = ["textColor", "highlight", "fontSize", "codeLanguage"];

    private RichTextVocabulary(
        IReadOnlyList<MarkKind> markOrder, TokenFamily textColor, TokenFamily highlight, TokenFamily fontSize, TokenFamily codeLanguage)
    {
        MarkOrder = markOrder;
        TextColor = textColor;
        Highlight = highlight;
        FontSize = fontSize;
        CodeLanguage = codeLanguage;
    }

    public IReadOnlyList<MarkKind> MarkOrder { get; }

    public TokenFamily TextColor { get; }

    public TokenFamily Highlight { get; }

    public TokenFamily FontSize { get; }

    public TokenFamily CodeLanguage { get; }

    public IEnumerable<TokenFamily> Families => [TextColor, Highlight, FontSize, CodeLanguage];

    /// <summary>The vocabulary embedded in this assembly.</summary>
    public static RichTextVocabulary LoadEmbedded()
    {
        using var stream = typeof(RichTextVocabulary).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' is missing.");

        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }

    /// <summary>The raw bytes of the embedded file (the drift test compares them with the file on disk).</summary>
    public static byte[] EmbeddedBytes()
    {
        using var stream = typeof(RichTextVocabulary).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' is missing.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }

    public static RichTextVocabulary Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var markOrder = new List<MarkKind>();

        foreach (var entry in root.GetProperty("markOrder").EnumerateArray())
        {
            var name = entry.GetString();
            var kind = Enum.GetValues<MarkKind>().Cast<MarkKind?>().FirstOrDefault(
                candidate => string.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"The vocabulary's markOrder names a mark the reduction does not implement: '{name}'.");

            if (markOrder.Contains(kind))
            {
                throw new InvalidOperationException($"The vocabulary's markOrder names '{name}' twice.");
            }

            markOrder.Add(kind);
        }

        if (markOrder.Count != Enum.GetValues<MarkKind>().Length)
        {
            throw new InvalidOperationException("The vocabulary's markOrder must name every mark exactly once.");
        }

        var families = new Dictionary<string, TokenFamily>(StringComparer.Ordinal);
        var allClasses = new HashSet<string>(StringComparer.Ordinal);

        foreach (var family in root.GetProperty("families").EnumerateArray())
        {
            var kind = family.GetProperty("kind").GetString() ?? string.Empty;

            if (!KnownKinds.Contains(kind, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"The vocabulary has a family kind the reduction does not implement: '{kind}'.");
            }

            if (families.ContainsKey(kind))
            {
                throw new InvalidOperationException($"The vocabulary has the family '{kind}' twice.");
            }

            var element = family.GetProperty("element").GetString() ?? string.Empty;
            var within = family.TryGetProperty("within", out var withinElement) ? withinElement.GetString() : null;

            var (expectedElement, expectedWithin, prefix) = kind switch
            {
                "textColor" => ("span", null, "rt-"),
                "highlight" => ("mark", null, "rt-"),
                "fontSize" => ("span", null, "rt-"),
                _ => ("code", "pre", string.Empty),
            };

            if (element != expectedElement || within != expectedWithin)
            {
                throw new InvalidOperationException($"The family '{kind}' must be bound to '{expectedElement}'.");
            }

            var tokens = new List<VocabularyToken>();

            foreach (var token in family.GetProperty("tokens").EnumerateArray())
            {
                var name = token.GetProperty("name").GetString() ?? string.Empty;
                var cls = token.GetProperty("class").GetString() ?? string.Empty;

                // The product's tokens share a reserved prefix; a code language's class ends with the language's name.
                var shapeOk = kind == "codeLanguage"
                    ? cls.EndsWith("-" + name, StringComparison.Ordinal)
                    : cls.StartsWith(prefix, StringComparison.Ordinal) && cls.Length > prefix.Length;

                if (!shapeOk || name.Length == 0 || cls.Any(char.IsWhiteSpace))
                {
                    throw new InvalidOperationException($"The class '{cls}' of '{kind}' is not one token of the shape the family has.");
                }

                if (!allClasses.Add(cls))
                {
                    throw new InvalidOperationException($"The class '{cls}' appears twice in the vocabulary.");
                }

                tokens.Add(new VocabularyToken(name, cls));
            }

            families[kind] = new TokenFamily(kind, element, within, tokens);
        }

        foreach (var known in KnownKinds)
        {
            if (!families.ContainsKey(known))
            {
                throw new InvalidOperationException($"The vocabulary has no family '{known}'.");
            }
        }

        return new RichTextVocabulary(
            markOrder, families["textColor"], families["highlight"], families["fontSize"], families["codeLanguage"]);
    }
}
