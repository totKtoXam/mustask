using Mustask.Application.Abstractions.Dictionaries;

namespace Mustask.Application.Tests.TestDoubles;

public sealed class FakeRequestLanguageResolver : IRequestLanguageResolver
{
    public ResolvedLanguage NextResult { get; set; } = new(Guid.NewGuid(), "ru", Guid.NewGuid(), "ru");

    public IReadOnlyList<string>? LastAcceptedTags { get; private set; }

    public Task<ResolvedLanguage> ResolveAsync(IReadOnlyList<string> acceptedTags, CancellationToken cancellationToken)
    {
        LastAcceptedTags = acceptedTags;
        return Task.FromResult(NextResult);
    }
}
