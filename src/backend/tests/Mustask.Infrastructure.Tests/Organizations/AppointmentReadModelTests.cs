using System.Diagnostics;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Infrastructure.Tests.Process;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// feat-033 plan-backend § 5 against a real PostgreSQL: the rescue list is one count and one page whatever the number of
/// organizations (NFR Volumes, within two seconds for 1000), the record of a hundred appointments names its people in a fixed
/// number of statements, the name filter folds case and «ё» in SQL, and the table's backstops (blank reason, 500 code points,
/// the restricting foreign key) refuse what the domain would never build.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class AppointmentReadModelTests(OrganizationsDatabaseFixture database)
{
    private static string Tag() => Guid.NewGuid().ToString("N");

    private async Task<Guid> InsertOrganizationAsync(string name, bool active = true)
    {
        var structure = await database.CreateStructureAsync(name);
        if (!active)
        {
            await database.ExecuteAsync("UPDATE organizations SET status = 'Deactivated' WHERE id = @id", ("id", structure.Organization.Id));
        }

        return structure.Organization.Id;
    }

    private async Task<TResult> ReadAsync<TResult>(CommandCountingInterceptor? counter, Func<IAppointmentReadModel, Task<TResult>> read)
    {
        await using var provider = counter is null ? database.BuildServices() : database.BuildServices(counter);
        await using var scope = provider.CreateAsyncScope();

        return await read(scope.ServiceProvider.GetRequiredService<IAppointmentReadModel>());
    }

    // The filter folds case and «ё» in SQL, treats % and _ literally and keeps the order by name.
    [Fact]
    public async Task Page_FiltersAtSqlLevel_CaseAndYo_AndPercentLiterally()
    {
        var tag = Tag();
        await InsertOrganizationAsync($"{tag} Ёлка");
        await InsertOrganizationAsync($"{tag} Гамма 100%");
        await InsertOrganizationAsync($"{tag} Гамма 1000");

        var yo = await ReadAsync(null, model => model.GetOrganizationsPageAsync($"{tag} ЕЛКА", 1, 25, CancellationToken.None));
        var percent = await ReadAsync(null, model => model.GetOrganizationsPageAsync($"{tag} гамма 100%", 1, 25, CancellationToken.None));

        Assert.Equal(1, yo.Count);
        Assert.Equal($"{tag} Ёлка", Assert.Single(yo.Rows).Name);
        Assert.Equal(1, percent.Count);
        Assert.Equal($"{tag} Гамма 100%", Assert.Single(percent.Rows).Name);
    }

    // NFR Volumes: 1000 organizations, the list page is a count plus a page, answered within two seconds.
    [Fact]
    public async Task Page_ThousandOrganizations_IsTwoStatements_WithinTwoSeconds()
    {
        var tag = Tag();
        await InsertOrganizationAsync($"{tag} 0000");
        await database.ExecuteAsync(
            "WITH n AS (SELECT gen_random_uuid() AS oid, gen_random_uuid() AS pid, i FROM generate_series(1, 999) i), "
            + "p AS (INSERT INTO processes (id, organization_id) SELECT pid, oid FROM n) "
            + "INSERT INTO organizations (id, name, normalized_name, status, process_id, created_at, created_by) "
            + "SELECT oid, @tag || ' ' || lpad(i::text, 4, '0'), upper(@tag || ' ' || lpad(i::text, 4, '0')), 'Active', pid, now(), 'seed' FROM n",
            ("tag", tag));
        var counter = new CommandCountingInterceptor();

        var stopwatch = Stopwatch.StartNew();
        var page = await ReadAsync(counter, model => model.GetOrganizationsPageAsync(tag, 3, 25, CancellationToken.None));
        stopwatch.Stop();

        Assert.Equal(1000, page.Count);
        Assert.Equal(25, page.Rows.Count);
        Assert.Equal(2, counter.Commands.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"The page took {stopwatch.Elapsed}.");
    }

    // 100 appointments by a few people: the record is one statement for the rows and one batch for the people.
    [Fact]
    public async Task List_HundredAppointments_IsTwoStatements_NewestFirst()
    {
        var tag = Tag();
        var organization = await InsertOrganizationAsync($"{tag} Журнал");
        await database.SeedUsersAsync(($"by-{tag}", "Назначил", "Системный", "", true), ($"to-{tag}", "Назначен", "Админ", "", true));
        await database.ExecuteAsync(
            "INSERT INTO organization_appointments (id, organization_id, appointee_id, appointed_by, appointed_at, reason) "
            + "SELECT gen_random_uuid(), @org, @to, @by, now() + (i || ' seconds')::interval, 'Причина ' || i FROM generate_series(1, 100) i",
            ("org", organization),
            ("to", $"to-{tag}"),
            ("by", $"by-{tag}"));
        var counter = new CommandCountingInterceptor();

        var rows = await ReadAsync(counter, model => model.ListAsync(organization, CancellationToken.None));

        Assert.Equal(100, rows.Count);
        Assert.Equal("Причина 100", rows[0].Reason);
        Assert.Equal("Причина 1", rows[^1].Reason);
        Assert.Equal(2, counter.Commands.Count);
    }

    [Fact]
    public async Task Page_ShowsTheLastAppointment_AndNullWhenNone()
    {
        var tag = Tag();
        var with = await InsertOrganizationAsync($"{tag} С записью");
        await InsertOrganizationAsync($"{tag} Без записи", active: false);
        await database.ExecuteAsync(
            "INSERT INTO organization_appointments (id, organization_id, appointee_id, appointed_by, appointed_at, reason) VALUES "
            + "(gen_random_uuid(), @org, 'u', 's', '2026-10-01T10:00:00Z', 'a'), (gen_random_uuid(), @org, 'u', 's', '2026-10-02T10:00:00Z', 'b')",
            ("org", with));

        var page = await ReadAsync(null, model => model.GetOrganizationsPageAsync(tag, 1, 25, CancellationToken.None));

        Assert.Equal(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero), page.Rows.Single(r => r.Id == with).LastAppointedAt);
        var without = page.Rows.Single(r => r.Id != with);
        Assert.Null(without.LastAppointedAt);
        Assert.False(without.IsActive);
    }

    // The table's backstops: a blank reason, 501 code points and an organization that still has records.
    [Fact]
    public async Task Table_RefusesABlankReason_AReasonOver500_AndTheDeletionOfAnOrganizationWithRecords()
    {
        var organization = await InsertOrganizationAsync($"{Tag()} Ограничения");
        const string insert =
            "INSERT INTO organization_appointments (id, organization_id, appointee_id, appointed_by, appointed_at, reason) "
            + "VALUES (gen_random_uuid(), @org, 'u', 's', now(), @reason)";

        var blank = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(insert, ("org", organization), ("reason", "   ")));
        var tooLong = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(insert, ("org", organization), ("reason", new string('я', 501))));
        await database.ExecuteAsync(insert, ("org", organization), ("reason", new string('я', 500)));
        var restricted = await Assert.ThrowsAsync<PostgresException>(
            () => database.ExecuteAsync("DELETE FROM organizations WHERE id = @org", ("org", organization)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, blank.SqlState);
        Assert.Equal("ck_organization_appointments_reason_not_blank", blank.ConstraintName);
        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, tooLong.SqlState);
        Assert.Equal(PostgresErrorCodes.RestrictViolation, restricted.SqlState);
    }
}
