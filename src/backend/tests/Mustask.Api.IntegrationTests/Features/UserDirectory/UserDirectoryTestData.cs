using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// The user projection's test data. The test classes of a
/// shard of <see cref="MustaskApiCollection"/> run one after another on one database, so a
/// test that reads the projection starts from <see cref="ResetAsync"/>: an
/// empty projection, whatever ran before it.
/// </summary>
internal static class UserDirectoryTestData
{
    public static async Task ResetAsync(MustaskApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE directory_users");
    }

    /// <summary>
    /// A person to put into the projection. Signing in makes a person present and enabled; the legacy states of the retired
    /// push channel (ADR-0025: disabled, deleted) are seeded on top of that, because rows written then are still read.
    /// </summary>
    public sealed record SeedUser(
        string Id,
        string? FirstName,
        string? LastName,
        string? Email,
        bool Enabled,
        string Username,
        bool Deleted);

    public static SeedUser Person(
        string id,
        string? firstName,
        string? lastName,
        string? email,
        bool enabled = true,
        string? username = null,
        bool deleted = false) =>
        new(id, firstName, lastName, email, enabled, username ?? $"user.{id}", deleted);

    /// <summary>Puts people into the projection directly — the read model's tests do not depend on a sign-in.</summary>
    public static async Task SeedAsync(MustaskApiFactory factory, IEnumerable<SeedUser> people)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        AddTo(context, people);
        await context.SaveChangesAsync();
    }

    /// <summary>The same rows, for a test that holds a context of its own.</summary>
    public static void AddTo(MustaskDbContext context, IEnumerable<SeedUser> people)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var person in people)
        {
            var user = ProjectedUser.SignIn(new SignInProfile(person.Id, person.Username, person.FirstName, person.LastName, person.Email), now);
            context.DirectoryUsers.Add(user);

            if (person.Enabled && !person.Deleted)
            {
                continue;
            }

            var entry = context.Entry(user);
            entry.Property(u => u.Enabled).CurrentValue = person.Enabled && !person.Deleted;
            entry.Property(u => u.Lifecycle).CurrentValue = person.Deleted ? ProjectedUserLifecycle.Deleted : ProjectedUserLifecycle.Present;
            entry.Property(u => u.IsActive).CurrentValue = false;
        }
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

    public static string NewId() => Guid.NewGuid().ToString();
}
