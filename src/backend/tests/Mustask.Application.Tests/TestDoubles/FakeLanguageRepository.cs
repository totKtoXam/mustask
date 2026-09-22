using System.Globalization;
using Mustask.Application.Abstractions.Dictionaries;
using Mustask.Domain.Dictionaries;

namespace Mustask.Application.Tests.TestDoubles;

/// <summary>
/// `_versions` simulates `xmin`: every entity starts at version 1, and
/// <see cref="SaveAsync"/> bumps every tracked entity by one, the same
/// observable effect a real write has on the row(s) it touches.
/// </summary>
public sealed class FakeLanguageRepository : ILanguageRepository
{
    private readonly Dictionary<Guid, Language> _byId = [];
    private readonly Dictionary<object, int> _versions = [];

    public int SaveCallCount { get; private set; }

    /// <summary>
    /// One entry per <see cref="SaveAsync"/> call,
    /// (code, isDefault) for every tracked language at that moment -- lets a
    /// test assert the *order* in which the default flag moves across
    /// separate flushes, not just how many flushes happened.
    /// </summary>
    public List<IReadOnlyList<(string Code, bool IsDefault)>> SaveSnapshots { get; } = [];

    public void Add(Language language) => _byId[language.Id] = language;

    public Task<Language?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_byId.GetValueOrDefault(id));

    public Task<Language?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(_byId.Values.SingleOrDefault(l => l.Code == code));

    public Task<IReadOnlyList<Language>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Language>>(_byId.Values.ToList());

    public Task AddAsync(Language language, CancellationToken cancellationToken)
    {
        _byId[language.Id] = language;
        return Task.CompletedTask;
    }

    public string GetVersion(Language language) => Version(language).ToString(CultureInfo.InvariantCulture);

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        SaveCallCount++;

        SaveSnapshots.Add(_byId.Values.Select(l => (l.Code, l.IsDefault)).ToList());

        foreach (var key in _versions.Keys.ToList())
        {
            _versions[key]++;
        }

        return Task.CompletedTask;
    }

    private int Version(object entity)
    {
        if (!_versions.TryGetValue(entity, out var version))
        {
            version = 1;
            _versions[entity] = version;
        }

        return version;
    }
}
