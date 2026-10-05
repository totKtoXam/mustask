using Mustask.Domain.Organizations;
using Mustask.Domain.Process;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>
/// api-contract § 0.2 (BR-M1-20): one definition of a well-formed id. It agrees
/// with mod-001's <see cref="OrganizationKey"/> on every value, so the
/// organization-context filter and the process read never disagree.
/// </summary>
public sealed class AddressedIdTests
{
    public static TheoryData<string?> Values() => new()
    {
        "d0000000-0000-4000-8000-000000000001",
        "D0000000-0000-4000-8000-00000000000A",
        "0198d2a4-1c3e-7f00-9abc-0123456789ab",
        null,
        "",
        "abc",
        "00000000-0000-0000-0000-000000000000",
        "d00000000000400080000000000000001",
        "{d0000000-0000-4000-8000-000000000001}",
        "(d0000000-0000-4000-8000-000000000001)",
        "d0000000-0000-4000-8000-00000000001",
        "d0000000-0000-4000-8000-000000000001a",
        " d0000000-0000-4000-8000-000000000001",
        "d0000000-0000-4000-8000-000000000001 ",
        "организация",
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void TryParse_AgreesWithOrganizationKey(string? value)
    {
        var addressed = AddressedId.TryParse(value, out var addressedId);
        var organization = OrganizationKey.TryParse(value, out var organizationId);

        Assert.Equal(organization, addressed);
        Assert.Equal(organizationId, addressedId);
    }

    [Theory]
    [InlineData("d0000000-0000-4000-8000-000000000001")]
    [InlineData("D0000000-0000-4000-8000-000000000001")]
    public void TryParse_CanonicalFormInEitherCase_IsTheSameId(string value)
    {
        Assert.True(AddressedId.TryParse(value, out var id));
        Assert.Equal(Guid.Parse("d0000000-0000-4000-8000-000000000001"), id);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("{d0000000-0000-4000-8000-000000000001}")]
    [InlineData("d00000000000400080000000000000001")]
    [InlineData("d0000000-0000-4000-8000-000000000001 ")]
    public void TryParse_AnyOtherForm_Fails(string value)
    {
        Assert.False(AddressedId.TryParse(value, out var id));
        Assert.Equal(Guid.Empty, id);
    }
}
