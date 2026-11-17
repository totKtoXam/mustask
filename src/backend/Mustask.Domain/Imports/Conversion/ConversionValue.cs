using System.Globalization;

namespace Mustask.Domain.Imports.Conversion;

public enum ConversionKind
{
    Empty,
    Text,
    Number,
    Date,
    List,
}

/// <summary>What a converter chain carries from step to step: nothing, text, a number, a date or a list of texts.</summary>
public sealed record ConversionValue(ConversionKind Kind, string Text = "", decimal Number = 0m, DateOnly Date = default, IReadOnlyList<string>? Items = null)
{
    public static ConversionValue Empty { get; } = new(ConversionKind.Empty);

    public static ConversionValue OfText(string text) => text.Length == 0 ? Empty : new(ConversionKind.Text, text);

    public static ConversionValue OfNumber(decimal number) => new(ConversionKind.Number, Number: number);

    public static ConversionValue OfDate(DateOnly date) => new(ConversionKind.Date, Date: date);

    public static ConversionValue OfList(IReadOnlyList<string> items) => items.Count == 0 ? Empty : new(ConversionKind.List, Items: items);

    public bool IsEmpty => Kind == ConversionKind.Empty;

    /// <summary>The canonical text: a comma decimal, <c>yyyy-MM-dd</c> dates, list items joined by «; ».</summary>
    public string Display() => Kind switch
    {
        ConversionKind.Text => Text,
        ConversionKind.Number => Number.ToString("0.############", CultureInfo.GetCultureInfo("ru-RU")),
        ConversionKind.Date => Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ConversionKind.List => string.Join("; ", Items ?? []),
        _ => string.Empty,
    };
}
