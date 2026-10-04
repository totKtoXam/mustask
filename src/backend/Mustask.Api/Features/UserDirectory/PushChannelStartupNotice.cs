using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.Features.UserDirectory;

/// <summary>
/// Says at startup, at Information, that <c>UserDirectory:Push:PreviousSecret</c>
/// is configured (security pass S4): while it is, a second secret is accepted
/// on the push channel, and ADR-0025 § 3's rotation ends with removing it — the
/// operator otherwise has no signal that the provider has moved to the new one.
/// Names the key, never a value.
/// </summary>
public sealed partial class PushChannelStartupNotice(PushChannelOptions options, ILogger<PushChannelStartupNotice> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.PreviousSecret is not null)
        {
            LogPreviousSecretConfigured(logger, PushChannelOptions.PreviousSecretKey);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "User directory push channel: {Key} is configured, so requests signed with the previous secret are accepted too. "
            + "Remove the key once the provider uses the new secret (ADR-0025 section 3).")]
    private static partial void LogPreviousSecretConfigured(ILogger logger, string key);
}
