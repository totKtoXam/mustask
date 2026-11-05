using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.GetRescueOrganizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetRescueOrganizations;

/// <summary>
/// <c>GetRescueOrganizations</c> on a fake read model (feat-033 api-contract § 1.1): the page is parsed and refused when below 1
/// or not a whole number, the name is trimmed (blank means no filter), the page size is 25 and the status is spelled out.
/// </summary>
public sealed class GetRescueOrganizationsHandlerTests
{
    private sealed class Reads : IAppointmentReadModel
    {
        public (string? Filter, int Page, int Size)? Asked { get; private set; }

        public Task<RescueOrganizationsPage> GetOrganizationsPageAsync(string? nameFilter, int page, int pageSize, CancellationToken cancellationToken)
        {
            Asked = (nameFilter, page, pageSize);

            return Task.FromResult(new RescueOrganizationsPage(
                30,
                [
                    new RescueOrganizationRowView(Guid.CreateVersion7(), "А", true, null),
                    new RescueOrganizationRowView(Guid.CreateVersion7(), "Б", false, DateTimeOffset.UnixEpoch),
                ]));
        }

        public Task<IReadOnlyList<AppointmentView>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AppointmentView?> FindAsync(Guid appointmentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> OrganizationExistsAsync(Guid organizationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("1", 1)]
    [InlineData("7", 7)]
    public async Task Handle_ValidPage_IsPassedOnWithPageSize25(string? page, int expected)
    {
        var reads = new Reads();

        var result = await new GetRescueOrganizationsHandler(reads).Handle(new GetRescueOrganizationsQuery(page, null), CancellationToken.None);

        Assert.Equal((null, expected, 25), reads.Asked);
        Assert.Equal(30, result.Count);
        Assert.Equal(["active", "deactivated"], result.Data.Select(row => row.Status));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    public async Task Handle_InvalidPage_IsValidationFailedOnPage_WithoutReading(string page)
    {
        var reads = new Reads();

        var refusal = await Assert.ThrowsAsync<ValidationException>(
            () => new GetRescueOrganizationsHandler(reads).Handle(new GetRescueOrganizationsQuery(page, null), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.RescueInvalidPage, Assert.Single(refusal.Errors["page"]).Code);
        Assert.Null(reads.Asked);
    }

    [Theory]
    [InlineData("  бет  ", "бет")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public async Task Handle_TheNameIsTrimmed_AndBlankMeansNoFilter(string? name, string? expected)
    {
        var reads = new Reads();

        await new GetRescueOrganizationsHandler(reads).Handle(new GetRescueOrganizationsQuery(null, name), CancellationToken.None);

        Assert.Equal(expected, reads.Asked!.Value.Filter);
    }
}
