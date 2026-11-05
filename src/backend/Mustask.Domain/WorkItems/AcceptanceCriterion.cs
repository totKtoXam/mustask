namespace Mustask.Domain.WorkItems;

/// <summary>
/// A single acceptance criterion scenario for a user story (R1),
/// following the Given-When-Then pattern.
/// </summary>
public sealed record AcceptanceCriterion(string Given, string When, string Then)
{
    public string Given { get; init; } = Given ?? string.Empty;
    public string When { get; init; } = When ?? string.Empty;
    public string Then { get; init; } = Then ?? string.Empty;
}
