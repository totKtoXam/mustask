using Mustask.Application.Features.Process;
using Mustask.Application.Features.Process.SeedProcess;
using Mustask.Domain.Process;

namespace Mustask.Application.Tests.Features.Process.SeedProcess;

/// <summary>US-1 (mod-001-process-seed): the seed seam as the handler implements it, with a fake store.</summary>
public sealed class SeedProcessHandlerTests
{
    private static readonly Guid OrganizationId = Guid.Parse("a1b2c3d4-0000-4000-8000-000000000001");

    private static (IProcessSeed Seed, FakeProcessSeedStore Store) NewSeed()
    {
        var store = new FakeProcessSeedStore();

        return (new SeedProcessHandler(store), store);
    }

    // US-1 · "A malformed identifier is refused and nothing is created": the store is never called.
    [Fact]
    public async Task SeedAsync_EmptyOrganizationId_ThrowsWithoutCallingTheStore()
    {
        var (seed, store) = NewSeed();

        await Assert.ThrowsAsync<ArgumentException>(() => seed.SeedAsync(Guid.Empty, CancellationToken.None));

        Assert.Empty(store.Candidates);
    }

    // US-1 · "A new organization identifier receives the complete seeded process": the store gets the complete candidate.
    [Fact]
    public async Task SeedAsync_WellFormedId_HandsTheStoreTheCompleteProcessOfThatOrganization()
    {
        var (seed, store) = NewSeed();

        await seed.SeedAsync(OrganizationId, CancellationToken.None);

        var candidate = Assert.Single(store.Candidates);
        Assert.Equal(OrganizationId, candidate.OrganizationId);
        Assert.Equal(4, candidate.BacklogLevels.Count);
        Assert.Equal(5, candidate.WorkItemTypes.Count);
        Assert.Equal(24, candidate.WorkItemTypes.Sum(type => type.States.Count));
    }

    // US-1 · "...the command hands back that process's identity": a new process answers with the candidate's identity.
    [Fact]
    public async Task SeedAsync_StoreCreatedTheProcess_ReturnsTheCandidatesIdentity()
    {
        var (seed, store) = NewSeed();

        var id = await seed.SeedAsync(OrganizationId, CancellationToken.None);

        Assert.Equal(store.Candidates.Single().Id, id);
    }

    // US-1 · "A repeated call creates nothing and hands back the same process": the existing identity wins over the candidate's.
    [Fact]
    public async Task SeedAsync_StoreAnswersWithTheExistingProcess_ReturnsThatIdentity()
    {
        var (seed, store) = NewSeed();
        var existing = ProcessId.New();
        store.AnswerWith = existing;

        var id = await seed.SeedAsync(OrganizationId, CancellationToken.None);

        Assert.Equal(existing, id);
        Assert.NotEqual(existing, store.Candidates.Single().Id);
    }

    // US-1 · "A failed seed leaves nothing behind and says so": the caller is told, and receives no identity.
    [Fact]
    public async Task SeedAsync_StoreThrows_PropagatesTheException()
    {
        var (seed, store) = NewSeed();
        var failure = new InvalidOperationException("storage failed");
        store.ThrowOnInsert = failure;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => seed.SeedAsync(OrganizationId, CancellationToken.None));

        Assert.Same(failure, thrown);
    }

    // US-1 · "Each organization identifier gets a process of its own": every call builds its own candidate.
    [Fact]
    public async Task SeedAsync_TwoOrganizations_GetTwoCandidatesWithNoSharedIdentity()
    {
        var (seed, store) = NewSeed();

        await seed.SeedAsync(OrganizationId, CancellationToken.None);
        await seed.SeedAsync(Guid.Parse("a1b2c3d4-0000-4000-8000-000000000002"), CancellationToken.None);

        Assert.Equal(2, store.Candidates.Count);
        Assert.NotEqual(store.Candidates[0].Id, store.Candidates[1].Id);
        Assert.NotEqual(store.Candidates[0].WorkItemTypes[0].Id, store.Candidates[1].WorkItemTypes[0].Id);
    }
}
