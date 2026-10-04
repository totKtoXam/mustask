using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// The user projection's test data. Every test class of
/// <see cref="MustaskApiCollection"/> runs sequentially on one database, so a
/// test that reads the projection starts from <see cref="ResetAsync"/>: an
/// empty projection, whatever ran before it.
/// </summary>
internal static class UserDirectoryTestData
{
    /// <summary>A moment comfortably in the past and well above zero, in microseconds.</summary>
    public const long BaseMoment = 1_700_000_000_000_000;

    public static async Task ResetAsync(MustaskApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE directory_snapshot_pages, directory_snapshots, directory_users");
    }

    /// <summary>A push as the provider would send it, for <see cref="SeedAsync"/> or the push routes.</summary>
    public static UserPush Push(
        string id,
        string? firstName,
        string? lastName,
        string? email,
        bool enabled = true,
        string? username = null,
        long moment = BaseMoment,
        bool deleted = false) =>
        new(id, moment, username ?? $"user.{id}", firstName, lastName, email, enabled, deleted);

    /// <summary>Puts users into the projection directly — the read model's tests do not depend on the push routes.</summary>
    public static async Task SeedAsync(MustaskApiFactory factory, IEnumerable<UserPush> pushes)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        var now = DateTimeOffset.UtcNow;
        var result = ProjectionBatch.Apply(pushes.ToList(), new Dictionary<string, ProjectedUser>(), now, snapshotId: null);

        context.DirectoryUsers.AddRange(result.Added);
        await context.SaveChangesAsync();
    }

    public static async Task<T> ReadAsync<T>(MustaskApiFactory factory, Func<IUserDirectory, Task<T>> read)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await read(scope.ServiceProvider.GetRequiredService<IUserDirectory>());
    }

    public static Task<UserSearchResult> SearchAsync(MustaskApiFactory factory, UserSearch search) =>
        ReadAsync(factory, directory => directory.SearchAsync(search, CancellationToken.None));

    public static Task<UserLookupResult> LookupAsync(MustaskApiFactory factory, params string[] ids) =>
        ReadAsync(factory, directory => directory.LookupAsync(ids, CancellationToken.None));

    /// <summary>The projection's row for one id, as stored.</summary>
    public static async Task<ProjectedUser?> FindAsync(MustaskApiFactory factory, string id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        return await context.DirectoryUsers.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id);
    }

    public static async Task<int> CountAsync(MustaskApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        return await context.DirectoryUsers.CountAsync();
    }

    public static async Task<int> SnapshotCountAsync(MustaskApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        return await context.DirectorySnapshots.CountAsync();
    }

    public static string NewId() => Guid.NewGuid().ToString();
}
