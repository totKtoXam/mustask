using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.Organizations;

// feat-033 BR-RS-6a rows 4b to 6 and their pairwise precedences.
public sealed class OrganizationAppointmentRuleTests
{
    private const string Caller = "caller";
    private const string User = "user";

    private static string Refusal(string userId, bool appointable, OrganizationRole? role) =>
        Assert.Throws<ConflictException>(() => OrganizationAppointmentRule.Decide(Caller, userId, appointable, role)).Code;

    [Fact]
    public void NotAppointable_IsRefused() =>
        Assert.Equal(OrganizationErrorCodes.RescueUserUnavailable, Refusal(User, false, null));

    [Fact]
    public void Self_IsRefused() =>
        Assert.Equal(OrganizationErrorCodes.RescueSelfAppointment, Refusal(Caller, true, null));

    [Fact]
    public void AlreadyAdmin_IsRefused() =>
        Assert.Equal(OrganizationErrorCodes.RescueAlreadyAdmin, Refusal(User, true, OrganizationRole.Admin));

    [Fact]
    public void UnknownAndSelf_AnswersUnavailable() =>
        Assert.Equal(OrganizationErrorCodes.RescueUserUnavailable, Refusal(Caller, false, null));

    [Fact]
    public void SelfAndAlreadyAdmin_AnswersSelf() =>
        Assert.Equal(OrganizationErrorCodes.RescueSelfAppointment, Refusal(Caller, true, OrganizationRole.Admin));

    [Fact]
    public void InactiveAndAlreadyAdmin_AnswersUnavailable() =>
        Assert.Equal(OrganizationErrorCodes.RescueUserUnavailable, Refusal(User, false, OrganizationRole.Admin));

    [Fact]
    public void NonMember_IsAdded() =>
        Assert.Equal(AppointmentEffect.Add, OrganizationAppointmentRule.Decide(Caller, User, true, null));

    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public void MemberOrViewer_IsRaised(OrganizationRole role) =>
        Assert.Equal(AppointmentEffect.Raise, OrganizationAppointmentRule.Decide(Caller, User, true, role));
}
