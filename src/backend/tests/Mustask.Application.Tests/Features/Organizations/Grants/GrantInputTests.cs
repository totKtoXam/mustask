using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.Grants;

// mod-019 api-contract § 2: the body's values are checked together, and a grant to oneself is refused (BR-SA-19).
public sealed class GrantInputTests
{
    private static readonly string Actor = Guid.CreateVersion7().ToString();

    private static readonly string Other = Guid.CreateVersion7().ToString();

    [Theory]
    [InlineData("reader", GrantRole.Reader)]
    [InlineData("member", GrantRole.Member)]
    [InlineData("manager", GrantRole.Manager)]
    public void Validate_AKnownRole_ReturnsTheGranteeAndTheRole(string text, GrantRole expected)
    {
        Assert.Equal((Other, expected), GrantInput.Validate(Other, text, Actor));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("viewer")]
    [InlineData("Manager")]
    public void Validate_AnUnknownRole_FailsOnTheRoleField(string? text)
    {
        var error = Assert.Throws<ValidationException>(() => GrantInput.Validate(Other, text, Actor));

        Assert.Equal(OrganizationErrorCodes.GrantRoleInvalid, error.Errors["role"][0].Code);
    }

    [Fact]
    public void Validate_TheCaller_FailsWithSelfGrantOnTheUserField()
    {
        var error = Assert.Throws<ValidationException>(() => GrantInput.Validate(Actor, "manager", Actor));

        Assert.Equal(OrganizationErrorCodes.GrantSelfGrant, error.Errors["userId"][0].Code);
    }

    [Fact]
    public void Validate_EveryFaultAtOnce_ReportsBothFields()
    {
        var error = Assert.Throws<ValidationException>(() => GrantInput.Validate("", "nope", Actor));

        Assert.Equal(["role", "userId"], error.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(ResourceScope.Area, OrganizationErrorCodes.AreaNotFound)]
    [InlineData(ResourceScope.Project, OrganizationErrorCodes.ProjectNotFound)]
    [InlineData(ResourceScope.Team, OrganizationErrorCodes.TeamNotFound)]
    public void NotFoundCodeOf_IsTheScopesOwn(ResourceScope scope, string expected)
    {
        Assert.Equal(expected, GrantObjects.NotFoundCodeOf(scope));
    }
}
