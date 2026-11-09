using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Kit;

/// <summary>
/// Two requests that meet in the database (tech-035): the first is held at its commit with its work written and its locks taken, the
/// second is sent and — by default — must be seen waiting on a lock, then the first is released and both answers are returned. The
/// choreography every race test wrote by hand on <see cref="CommitGate"/> and <see cref="PgLocks"/>.
/// </summary>
public static class Race
{
    /// <summary>
    /// <paramref name="first"/> gets the gate to send with its request; <paramref name="second"/> is sent while the first waits.
    /// <paramref name="secondWaits"/> is whether the second must block on the first's uncommitted work: <c>false</c> when the
    /// scenario's second request is refused or finishes without waiting for it.
    /// </summary>
    public static async Task<(Answer First, Answer Second)> TwoAsync(
        MustaskApiFactory factory, Func<CommitGate, Task<Answer>> first, Func<Task<Answer>> second, bool secondWaits = true)
    {
        using var gate = factory.CommitGates.Open();

        var firstRequest = first(gate);
        await Task.WhenAny(gate.Reached, firstRequest);
        Assert.False(firstRequest.IsCompleted, "The first request finished before reaching its commit gate.");

        var secondRequest = second();
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, secondRequest);

        if (secondWaits)
        {
            Assert.False(secondRequest.IsCompleted, "The second request did not wait for the first one's uncommitted work.");
        }

        gate.Release();

        return (await firstRequest, await secondRequest);
    }
}
