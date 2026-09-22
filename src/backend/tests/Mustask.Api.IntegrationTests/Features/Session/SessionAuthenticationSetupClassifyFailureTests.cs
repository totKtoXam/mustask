using Mustask.Api.Features.Session;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// Unit-level (no host) coverage of
/// <see cref="SessionAuthenticationSetup.ClassifyFailure"/> — the callback's
/// discovery-unreachable case used to fall through to <c>sign_in_failed</c>
/// instead of <c>idp_unavailable</c> because the real
/// <c>ConfigurationManager&lt;T&gt;</c> throws
/// <see cref="InvalidOperationException"/>, not <see cref="HttpRequestException"/>,
/// when it has never once obtained discovery — a distinction none of the
/// integration test doubles reproduce faithfully.
/// </summary>
public sealed class SessionAuthenticationSetupClassifyFailureTests
{
    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(InvalidOperationException))]
    public void DiscoveryOrNetworkFailures_ClassifiedAsIdpUnavailable(Type exceptionType)
    {
        var failure = (Exception)Activator.CreateInstance(exceptionType, "simulated")!;

        Assert.Equal("idp_unavailable", SessionAuthenticationSetup.ClassifyFailure(failure));
    }

    [Fact]
    public void UnrecognizedFailure_ClassifiedAsSignInFailed()
    {
        Assert.Equal("sign_in_failed", SessionAuthenticationSetup.ClassifyFailure(new InvalidCastException("simulated")));
    }

    [Fact]
    public void NullFailure_ClassifiedAsSignInFailed()
    {
        Assert.Equal("sign_in_failed", SessionAuthenticationSetup.ClassifyFailure(null));
    }
}
