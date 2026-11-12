using System.Net;
using System.Net.Http.Json;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-033 BR-RS-5 / US-3 "The record and the membership stand or fall together": the membership and the record are flushed by one
/// <c>SaveChanges</c>. A decorated record repository adds a second record that breaks a foreign key in that same save; the answer
/// is 500 and neither the membership change nor any record remains.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AppointmentRecordAtomicityTests(MustaskApiFactory factory)
{
    [Fact]
    public async Task Appoint_WhenTheRecordCannotBeSaved_Returns500_AndTheMembershipIsNotChanged()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var viewer = await RescueTestData.SignedInUserAsync(factory, "Атом", "Читатель");
        await MembershipTestData.AddMemberAsync(factory, organization.Id, viewer, OrganizationRole.Viewer);
        var newcomer = await RescueTestData.SignedInUserAsync(factory, "Атом", "Новичок");
        var (_, token) = RescueTestData.NewRescuer();
        var before = await RescueTestData.MembershipsAsync(factory, organization.Id);

        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IAppointmentRecordRepository>(provider =>
                    new RecordWithBrokenSibling(provider.GetRequiredService<MustaskDbContext>()))));
        var client = host.CreateClient();

        foreach (var userId in new[] { viewer, newcomer })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, RescueTestData.AppointmentsPath(organization.Id))
            {
                Content = JsonContent.Create(new { userId, reason = "Причина" }),
            };
            request.WithBearer(token);
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        Assert.Equal(before, await RescueTestData.MembershipsAsync(factory, organization.Id));
        Assert.Empty(await RescueTestData.RecordsAsync(factory, organization.Id));
    }

    /// <summary>Adds the real record and one more for an organization that does not exist: the foreign key fails in the same save.</summary>
    private sealed class RecordWithBrokenSibling(MustaskDbContext context) : IAppointmentRecordRepository
    {
        public void Add(OrganizationAppointment appointment)
        {
            context.OrganizationAppointments.Add(appointment);
            AppointmentReason.TryCreate("Второй", out var reason, out _);
            context.OrganizationAppointments.Add(OrganizationAppointment.Record(
                Guid.CreateVersion7(), appointment.AppointeeId, appointment.AppointedBy, reason, appointment.AppointedAt));
        }
    }
}
