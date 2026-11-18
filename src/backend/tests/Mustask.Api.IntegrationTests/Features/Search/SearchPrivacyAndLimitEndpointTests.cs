using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mustask.Api.Features.Search;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Search;

/// <summary>
/// feat-071 · feat-065 BR-GS-10 and BR-GS-15: the typed text never reaches a log record (the 200, the 400 and the 429 paths), and
/// the limiter's refusal travels as <c>429 http.too_many_requests</c> with <c>Retry-After: 1</c>. The host is built from the shared
/// factory with a capturing logger provider, so the class runs apart from the shards (<c>ApiTestCollectionFactory.SerialClasses</c>).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SearchPrivacyAndLimitEndpointTests(MustaskApiFactory factory)
{
    // A text no fixture, route, id or framework message contains.
    private const string Secret = "жгучийсекрет";

    private sealed class CapturingProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Records { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Capturing(categoryName, Records);

        public void Dispose()
        {
        }

        private sealed class Capturing(string category, ConcurrentQueue<string> records) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull =>
                null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var structured = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? string.Join(' ', pairs.Select(pair => $"{pair.Key}={pair.Value}"))
                    : string.Empty;

                records.Enqueue($"{category} {formatter(state, exception)} {structured} {exception}");
            }
        }
    }

    private async Task<(Guid OrganizationId, MembershipTestData.TestMember Member)> OrgAsync()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Логистика");
        await SearchTestKit.ProjectAsync(factory, organization.Id, department, "Альфа", "ALP");

        return (organization.Id, member);
    }

    private static Task<Answer> PostAsync(WebApplicationFactory<Program> host, Guid organizationId, string token, string? query, string? expand = null) =>
        OrganizationsApi.SendAsync(host, HttpMethod.Post, SearchTestKit.Path(organizationId), new { query, expand }, token);

    // BR-GS-10: the text of a read that finds nothing, of one that is refused as too long, of an unknown group and of one the limiter
    // refuses is in no record of any logger.
    [Fact]
    public async Task TheTypedText_IsInNoLogRecord_OnAnyPath()
    {
        var (organizationId, member) = await OrgAsync();
        var capture = new CapturingProvider();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(capture)));

        var ok = await PostAsync(host, organizationId, member.Token, Secret);
        var expanded = await PostAsync(host, organizationId, member.Token, Secret + "ж", "teams");
        var tooLong = await PostAsync(host, organizationId, member.Token, Secret + new string('ж', 100));
        var badGroup = await PostAsync(host, organizationId, member.Token, Secret, "nonsense");

        var limiter = host.Services.GetRequiredService<SearchReadLimiter>();
        var held = new List<System.Threading.RateLimiting.RateLimitLease>();

        for (var index = 0; index < SearchReadLimiter.PerUserPermits; index++)
        {
            held.Add((await limiter.TryEnterAsync(member.Id, CancellationToken.None))!);
        }

        var queued = Enumerable.Range(0, SearchReadLimiter.PerUserQueue)
            .Select(_ => limiter.TryEnterAsync(member.Id, CancellationToken.None).AsTask()).ToList();
        var refused = await PostAsync(host, organizationId, member.Token, Secret);

        foreach (var lease in held)
        {
            lease.Dispose();
        }

        await Task.WhenAll(queued);

        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.Equal(HttpStatusCode.OK, expanded.Status);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.Status);
        Assert.Equal(HttpStatusCode.BadRequest, badGroup.Status);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.Status);
        Assert.NotEmpty(capture.Records);
        Assert.DoesNotContain(capture.Records, record => record.Contains("жгучийсекрет", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tooLong.Body.GetRawText(), Secret, StringComparison.Ordinal);
        Assert.DoesNotContain(badGroup.Body.GetRawText(), Secret, StringComparison.Ordinal);
    }

    // BR-GS-15: the refusal is 429 http.too_many_requests with Retry-After: 1, and the read is not run.
    [Fact]
    public async Task ARefusedRead_Is429WithRetryAfterOne()
    {
        var (organizationId, member) = await OrgAsync();
        var limiter = factory.Services.GetRequiredService<SearchReadLimiter>();
        var held = new List<System.Threading.RateLimiting.RateLimitLease>();

        for (var index = 0; index < SearchReadLimiter.PerUserPermits; index++)
        {
            held.Add((await limiter.TryEnterAsync(member.Id, CancellationToken.None))!);
        }

        var queued = Enumerable.Range(0, SearchReadLimiter.PerUserQueue)
            .Select(_ => limiter.TryEnterAsync(member.Id, CancellationToken.None).AsTask()).ToList();

        using var request = new HttpRequestMessage(HttpMethod.Post, SearchTestKit.Path(organizationId))
        {
            Content = JsonContent.Create(new { query = "альфа" }),
        };
        request.WithBearer(member.Token);
        using var response = await factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        foreach (var lease in held)
        {
            lease.Dispose();
        }

        await Task.WhenAll(queued);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("1", response.Headers.GetValues("Retry-After").Single());
        Assert.Contains("http.too_many_requests", body, StringComparison.Ordinal);
    }
}
