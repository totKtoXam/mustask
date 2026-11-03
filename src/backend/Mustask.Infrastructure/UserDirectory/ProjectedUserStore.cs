using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.UserDirectory;

/// <summary><see cref="IProjectedUserStore"/> over <c>directory_users</c>.</summary>
public sealed class ProjectedUserStore(MustaskDbContext context) : IProjectedUserStore
{
    public async Task<ProjectedUser?> LockAndLoadAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        string[] ids = [id];

        await UserDirectoryLocks.LockUsersAsync(context, ids, cancellationToken);
        await UserDirectoryLocks.LockHeldRowsAsync(context, ids, cancellationToken);

        return await context.DirectoryUsers.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public void Add(ProjectedUser user) => context.DirectoryUsers.Add(user);
}
