using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using static Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData;

namespace Mustask.Domain.Tests.Organizations;

public sealed class OrganizationTests
{
    // US-1 · "An organization is created with its process": active, named, with the seeded process and its creator.
    [Fact]
    public void Create_IsActiveWithItsProcessAndCreator()
    {
        var id = Guid.CreateVersion7();
        var processId = ProcessId.New();

        var organization = Organization.Create(id, Name("  Пример организации "), processId, Now, Creator);

        Assert.Equal(id, organization.Id);
        Assert.Equal("Пример организации", organization.Name);
        Assert.Equal("ПРИМЕР ОРГАНИЗАЦИИ", organization.NormalizedName);
        Assert.Equal(OrganizationStatus.Active, organization.Status);
        Assert.True(organization.IsActive);
        Assert.Equal(processId, organization.ProcessId);
        Assert.Equal(Now, organization.CreatedAt);
        Assert.Equal(Creator, organization.CreatedBy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutCreator_IsADefect(string creator)
    {
        Assert.Throws<ArgumentException>(() => Organization.Create(Guid.CreateVersion7(), Name("Орг"), ProcessId.New(), Now, creator));
    }

    [Fact]
    public void Create_EmptyId_IsADefect()
    {
        Assert.Throws<ArgumentException>(() => Organization.Create(Guid.Empty, Name("Орг"), ProcessId.New(), Now, Creator));
    }

    // US-2 · "The organization is renamed".
    [Fact]
    public void Rename_ChangesNameAndKey()
    {
        var organization = AnOrganization();

        organization.Rename(Name("Новое имя"));

        Assert.Equal("Новое имя", organization.Name);
        Assert.Equal("НОВОЕ ИМЯ", organization.NormalizedName);
    }

    // US-2 · "deactivated" and "restored exactly as it was": one status, back and forth; restoring an active one is a no-op.
    [Fact]
    public void DeactivateThenRestore_TogglesTheStatusOnly()
    {
        var organization = AnOrganization();

        organization.Deactivate();
        Assert.Equal(OrganizationStatus.Deactivated, organization.Status);
        Assert.False(organization.IsActive);

        organization.Restore();
        Assert.Equal(OrganizationStatus.Active, organization.Status);

        organization.Restore();
        Assert.Equal(OrganizationStatus.Active, organization.Status);
        Assert.Equal("Пример организации", organization.Name);
    }
}
