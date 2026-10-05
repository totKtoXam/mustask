using System.Collections.Concurrent;
using Mustask.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// The registry of named commit gates (mod-002 plan-backend § 8.3). A test opens
/// a gate, sends request A with the gate's name in <see cref="Header"/>, waits
/// until A has reached it (A has done its work and holds every lock it took),
/// sends request B, waits until B blocks or finishes (<see cref="PgLocks"/>),
/// then releases A and asserts both outcomes.
/// </summary>
public sealed class CommitGates
{
    /// <summary>The request header that names the gate a request waits at before its commit.</summary>
    public const string Header = "X-Test-Commit-Gate";

    private readonly ConcurrentDictionary<string, CommitGate> _gates = new(StringComparer.Ordinal);

    /// <summary>A fresh gate under a unique name. Dispose it to release a request still waiting and forget the name.</summary>
    public CommitGate Open()
    {
        var gate = new CommitGate(Guid.NewGuid().ToString("N"), this);
        _gates[gate.Name] = gate;

        return gate;
    }

    internal CommitGate? Find(string name) => _gates.GetValueOrDefault(name);

    internal void Forget(CommitGate gate) => _gates.TryRemove(gate.Name, out _);
}

/// <summary>One gate: a request reaches it, and stays until the test releases it.</summary>
public sealed class CommitGate : IDisposable
{
    /// <summary>A request is never held longer than this, so a failed test does not hang the collection.</summary>
    private static readonly TimeSpan MaximumHold = TimeSpan.FromSeconds(60);

    private readonly CommitGates _owner;
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal CommitGate(string name, CommitGates owner)
    {
        Name = name;
        _owner = owner;
    }

    public string Name { get; }

    /// <summary>Completes when a request has done its work and waits at the gate, its transaction open.</summary>
    public Task Reached => _reached.Task;

    /// <summary>Lets the waiting request commit.</summary>
    public void Release() => _released.TrySetResult();

    public void Dispose()
    {
        Release();
        _owner.Forget(this);
    }

    internal async Task PassAsync()
    {
        _reached.TrySetResult();
        await _released.Task.WaitAsync(MaximumHold);
    }
}

/// <summary>
/// A test-only decorator of the unit of work: when the request names an open
/// gate, it awaits the gate after the handler's work and before the final save
/// and commit; otherwise it is the real unit of work, unchanged.
/// </summary>
public sealed class CommitGateUnitOfWork(IUnitOfWork inner, CommitGates gates, IHttpContextAccessor httpContextAccessor) : IUnitOfWork
{
    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken)
    {
        var name = httpContextAccessor.HttpContext?.Request.Headers[CommitGates.Header].ToString();

        if (string.IsNullOrEmpty(name) || gates.Find(name) is not { } gate)
        {
            return inner.ExecuteInTransactionAsync(action, cancellationToken);
        }

        return inner.ExecuteInTransactionAsync(
            async ct =>
            {
                var result = await action(ct);
                await gate.PassAsync();

                return result;
            },
            cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => inner.SaveChangesAsync(cancellationToken);
}
