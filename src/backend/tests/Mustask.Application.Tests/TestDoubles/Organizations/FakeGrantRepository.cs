using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>A grant repository for read-handler tests: it answers the project's inheritance (none by default) and refuses everything else.</summary>
public sealed class FakeGrantRepository(ProjectInheritance? inheritance = null) : IGrantRepository
{
    public Task<ProjectInheritance?> FindProjectInheritanceAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(inheritance);

    public Task<GrantObjectState> ResolveAsync(ResourceScope scope, Guid organizationId, Guid objectId, bool lockRow, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<GrantRecord>> ListAsync(ResourceScope scope, Guid objectId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<GrantRecord?> FindAsync(ResourceScope scope, Guid objectId, string userId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task AddAsync(ResourceScope scope, Guid organizationId, Guid objectId, string userId, GrantRole role, string grantedBy, DateTimeOffset now, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> ChangeRoleAsync(ResourceScope scope, Guid objectId, string userId, GrantRole role, string grantedBy, DateTimeOffset now, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> RemoveAsync(ResourceScope scope, Guid objectId, string userId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
