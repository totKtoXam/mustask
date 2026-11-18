namespace Mustask.Domain.Search;

/// <summary>The match tiers of BR-GS-8: a lower number ranks higher. The numbers are what the read stores and orders by.</summary>
public static class MatchTier
{
    /// <summary>The name, key or email equals the query.</summary>
    public const int Exact = 1;

    /// <summary>The name, key or email starts with the query.</summary>
    public const int Prefix = 2;

    /// <summary>A word of the name starts with the query.</summary>
    public const int WordPrefix = 3;

    /// <summary>The name contains the query.</summary>
    public const int Contains = 4;

    /// <summary>Trigram-similar (BR-GS-17): always below every other tier.</summary>
    public const int Similar = 5;
}
