using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Process;

namespace Mustask.Application.Tests.Features.Process.SeedProcess;

/// <summary>Records what the handler hands to the store and answers with a configured identity.</summary>
public sealed class FakeProcessSeedStore : IProcessSeedStore
{
    public List<ProcessDefinition> Candidates { get; } = [];

    /// <summary>The identity the store answers with. <c>null</c> — the candidate's own, as for a process that did not exist.</summary>
    public ProcessId? AnswerWith { get; set; }

    public Exception? ThrowOnInsert { get; set; }

    public Task<ProcessId> InsertIfAbsentAsync(ProcessDefinition candidate, CancellationToken cancellationToken)
    {
        Candidates.Add(candidate);

        if (ThrowOnInsert is not null)
        {
            throw ThrowOnInsert;
        }

        return Task.FromResult(AnswerWith ?? candidate.Id);
    }
}
