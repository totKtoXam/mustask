using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.AppointAdministrator;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.AppointAdministrator;

/// <summary>
/// <c>AppointAdministrator</c> on fakes (feat-033 plan-backend § 3): the order of refusals (BR-RS-6a), that no refusal writes
/// anything, the lock order (BR-RS-11) and what a success writes. The instance right before the handler is the policy's (Api tests).
/// </summary>
public sealed class AppointAdministratorHandlerTests
{
    private sealed class Records : IAppointmentRecordRepository
    {
        public List<OrganizationAppointment> Added { get; } = [];

        public void Add(OrganizationAppointment appointment) => Added.Add(appointment);
    }

    private sealed class Reads : IAppointmentReadModel
    {
        public Task<RescueOrganizationsPage> GetOrganizationsPageAsync(string? nameFilter, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AppointmentView>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AppointmentView?> FindAsync(Guid appointmentId, CancellationToken cancellationToken) =>
            Task.FromResult<AppointmentView?>(new AppointmentView(
                appointmentId,
                DateTimeOffset.UnixEpoch,
                new UserRefView("r", "Системный администратор", null, "active"),
                new UserRefView("u", "Кто-то", null, "active"),
                "причина"));

        public Task<bool> OrganizationExistsAsync(Guid organizationId, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            World.User.SubjectId = "rescuer";
            World.AddOutsider("user-vera", "Вера Иванова");
            Handler = new AppointAdministratorHandler(
                World.Organizations, World.Memberships, Records, new Reads(), World.Directory, World.Clock, World.User, new FakeUnitOfWork());
        }

        public MembershipWorld World { get; } = new();

        public Records Records { get; } = new();

        public AppointAdministratorHandler Handler { get; }

        public AppointAdministratorCommand Command(string? userId = "user-vera", string? reason = "Борис уволился", string? organizationId = null) =>
            new(organizationId ?? World.OrganizationId, userId, reason);

        public Task<AppointAdministratorResult> HandleAsync(AppointAdministratorCommand command) => Handler.Handle(command, CancellationToken.None);

        public void AssertNothingWritten()
        {
            Assert.Empty(Records.Added);
            Assert.Equal(0, World.Store.SaveCount);
        }
    }

    // US-2: a non-member is added as an administrator by the rescuer; one record; the locks are taken organization first.
    [Fact]
    public async Task Handle_ANonMember_IsAddedAsAdmin_WithOneRecord_LocksOrganizationThenMember_SavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(reason: "  Борис уволился "));

        Assert.Equal(OrganizationRole.Admin, f.World.RoleOf("user-vera"));
        var record = Assert.Single(f.Records.Added);
        Assert.Equal(("user-vera", "rescuer", "Борис уволился"), (record.AppointeeId, record.AppointedBy, record.Reason));
        Assert.Equal(
            [$"lock organization {f.World.Organization.Id}", "lock member user-vera", "save"],
            f.World.Store.Calls.Where(call => call.StartsWith("lock") || call == "save"));
    }

    // US-2 "raises a member": the role is raised, "added by" is kept, "role changed" names the rescuer.
    [Fact]
    public async Task Handle_AViewer_IsRaised_KeepingAddedBy()
    {
        var f = new Fixture();
        f.World.AddMember("user-viewer", "Нина Читателева", OrganizationRole.Viewer);
        var addedBy = f.World.Store.FindMembership(f.World.Organization.Id, "user-viewer")!.AddedBy;

        await f.HandleAsync(f.Command("user-viewer"));

        var after = f.World.Store.FindMembership(f.World.Organization.Id, "user-viewer")!;
        Assert.Equal((OrganizationRole.Admin, addedBy, "rescuer"), (after.Role, after.AddedBy, after.RoleChangedBy));
        Assert.Single(f.Records.Added);
    }

    // BR-RS-6a: the first applicable refusal answers, in this order, and none of them writes.
    [Fact]
    public async Task Handle_UnknownOrganization_IsNotFound_BeforeAnythingElse()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(
            () => f.HandleAsync(f.Command("never-signed-in", reason: "", organizationId: Guid.CreateVersion7().ToString())));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        f.AssertNothingWritten();
    }

    [Fact]
    public async Task Handle_MalformedOrganization_IsNotFound()
    {
        var f = new Fixture();

        await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(organizationId: "nope")));
        f.AssertNothingWritten();
    }

    [Theory]
    [InlineData("never-signed-in", "", OrganizationErrorCodes.RescueUserUnavailable)]
    [InlineData("user-inactive", "", OrganizationErrorCodes.RescueUserUnavailable)]
    [InlineData("rescuer", "", OrganizationErrorCodes.RescueSelfAppointment)]
    [InlineData("user-admin", "", OrganizationErrorCodes.RescueAlreadyAdmin)]
    public async Task Handle_UserRefusals_AnswerBeforeTheReason_AndWriteNothing(string userId, string reason, string code)
    {
        var f = new Fixture();
        f.World.AddOutsider("user-inactive", "Старая Строка", isActive: false);
        f.World.AddOutsider("rescuer", "Системный Администратор");
        f.World.AddMember("user-admin", "Анна Админова", OrganizationRole.Admin);

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(userId, reason)));

        Assert.Equal(code, refusal.Code);
        f.AssertNothingWritten();
    }

    [Theory]
    [InlineData(null, OrganizationErrorCodes.RescueReasonRequired)]
    [InlineData("   ", OrganizationErrorCodes.RescueReasonRequired)]
    [InlineData("a\0b", OrganizationErrorCodes.RescueReasonInvalid)]
    public async Task Handle_ABadReason_IsValidationFailedOnTheReasonField_AndWritesNothing(string? reason, string code)
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(reason: reason)));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(code, Assert.Single(refusal.Errors["reason"]).Code);
        f.AssertNothingWritten();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Handle_AMissingUser_IsValidationFailedOnTheUserField(string? userId)
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(userId)));

        Assert.Equal(OrganizationErrorCodes.MembershipUserInvalid, Assert.Single(refusal.Errors["userId"]).Code);
        f.AssertNothingWritten();
    }
}
