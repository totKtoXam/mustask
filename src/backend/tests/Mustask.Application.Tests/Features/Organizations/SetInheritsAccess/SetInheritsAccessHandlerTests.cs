using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.SetInheritsAccess;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.SetInheritsAccess;

/// <summary>mod-019 review C-8: after the lock the switch re-judges the parent's code and the node's own.</summary>
public sealed class SetInheritsAccessHandlerTests
{
    /// <summary>A scripted access: the answer for <c>area.write</c> on an object is whatever <see cref="Answer"/> says.</summary>
    private sealed class ScriptedAccess : IOrganizationAccess
    {
        public Func<Guid, bool> Answer { get; set; } = _ => true;

        public OrganizationRole Role => OrganizationRole.Member;

        public bool Holds(string code) => true;

        public Task<bool> HoldsOnAsync(string code, Guid objectId, CancellationToken cancellationToken) => Task.FromResult(Answer(objectId));
    }

    private static (SetInheritsAccessHandler Handler, SetInheritsAccessCommand Command, Area Node, InMemoryOrganizations Store, ScriptedAccess Access) Build()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var department = store.AddDepartment(organization, "Разработка");
        var node = store.RootOf(store.AddProject(organization, "Мастаск", "MST", department).Project);
        var access = new ScriptedAccess();
        var handler = new SetInheritsAccessHandler(
            new FakeOrganizationReadModel(store), new FakeProjectRepository(store), new FakeAreaRepository(store), access, new FakeUnitOfWork());

        return (handler, new SetInheritsAccessCommand(organization.Id.ToString(), node.Id.ToString(), false), node, store, access);
    }

    // C-8 · the route judged the node before the lock; a node whose own code no longer holds on the locked row is refused 403 and unchanged.
    [Fact]
    public async Task Handle_TheNodesOwnCodeIsLostAfterTheLock_Is403_NothingIsSaved()
    {
        var (handler, command, node, store, access) = Build();

        // The parent (judged before the lock) holds; the node's own code is only asked after the lock, and is gone there.
        access.Answer = id => id != node.Id;

        var thrown = await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, thrown.Code);
        Assert.True(node.InheritsAccess);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task Handle_BothCodesHoldAfterTheLock_SwitchesAndSaves()
    {
        var (handler, command, node, store, _) = Build();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.InheritsAccess);
        Assert.False(node.InheritsAccess);
        Assert.Equal(1, store.SaveCount);
    }
}
